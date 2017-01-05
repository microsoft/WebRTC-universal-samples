//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Org.WebRtc;

namespace ChatterBox.Background
{
    public sealed class StatsManager
    {
        private readonly TelemetryClient _telemetry;
        private Stopwatch _callWatch;

        private bool _isStatsCollectionEnabled;
        private AudioVideoMetricsCollector _metricsCollector;
        private Timer _metricsTimer;
        private Timer _networkTimer;

        private RTCPeerConnection _peerConnection;

        public StatsManager()
        {
            _telemetry = new TelemetryClient();
            _telemetry.Context.Session.Id = Guid.NewGuid().ToString();
        }

        public bool IsStatsCollectionEnabled
        {
            get { return _isStatsCollectionEnabled; }
            set
            {
                _isStatsCollectionEnabled = value;
                if (_peerConnection != null)
                {
                    _peerConnection.RtcStatsEnabled = value;
                    if (_isStatsCollectionEnabled)
                    {
                        var autoEvent = new AutoResetEvent(false);
                        _metricsCollector = new AudioVideoMetricsCollector(_telemetry);
                        TimerCallback tcb = _metricsCollector.TrackMetrics;
                        _metricsTimer = new Timer(tcb, autoEvent, 60000, 60000);
                    }
                    else
                    {
                        Reset();
                    }
                }
                else
                {
                    Debug.WriteLine("StatsManager: Stats are not toggled as manager is not initialized yet.");
                }
            }
        }

        public void DisableTelemetry(bool disable)
        {
            TelemetryConfiguration.Active.DisableTelemetry = disable;
        }

        public void Initialize(RTCPeerConnection pc)
        {
            if (pc != null)
            {
                _peerConnection = pc;
                _peerConnection.OnRTCStatsReportsReady += PeerConnection_OnRTCStatsReportsReady;
            }
            else
            {
                Debug.WriteLine("StatsManager: Cannot initialize peer connection by null pointer");
            }
        }

        public void Reset()
        {
            if (_peerConnection != null)
            {
                _peerConnection.RtcStatsEnabled = false;
                _peerConnection = null;
            }
            _metricsTimer?.Dispose();
        }

        public void StartCallWatch()
        {
            _telemetry.Context.Operation.Name = "Call Duration tracking";

            _callWatch = Stopwatch.StartNew();

            var autoEvent = new AutoResetEvent(false);
            Debug.Assert(_metricsCollector != null);
            TimerCallback tcb = _metricsCollector.CollectNewtorkMetrics;
            _networkTimer = new Timer(tcb, autoEvent, 0, 20000);
        }

        public void StopCallWatch()
        {
            if (_callWatch != null)
            {
                _callWatch.Stop();
                var currentDateTime = DateTime.Now;
                var time = _callWatch.Elapsed;
                Task.Run(() => _telemetry.TrackRequest("Call Duration", currentDateTime,
                    time,
                    "200", true)); // Response code, success
                if (_metricsCollector != null)
                {
                    _metricsCollector.TrackCurrentDelayMetrics();
                    _metricsCollector.TrackNetworkQualityMetrics();
                }
                _networkTimer?.Dispose();
            }
        }

        public void TrackEvent(string name)
        {
            Task.Run(() => _telemetry.TrackEvent(name));
        }

        public void TrackEvent(string name, IDictionary<string, string> props)
        {
            if (props == null)
            {
                Task.Run(() => _telemetry.TrackEvent(name));
            }
            else
            {
                props.Add("Timestamp", DateTimeOffset.UtcNow.ToString(@"hh\:mm\:ss"));
                Task.Run(() => _telemetry.TrackEvent(name, props));
            }
        }

        public void TrackException(Exception e)
        {
            var excTelemetry = new ExceptionTelemetry(e)
            {
                SeverityLevel = SeverityLevel.Critical,
                HandledAt = ExceptionHandledAt.Unhandled,
                Timestamp = DateTimeOffset.UtcNow
            };
            _telemetry.TrackException(excTelemetry);
        }

        public void TrackMetric(string name, double value)
        {
            var metric = new MetricTelemetry(name, value) {Timestamp = DateTimeOffset.UtcNow};
            Task.Run(() => _telemetry.TrackMetric(metric));
        }

        private void PeerConnection_OnRTCStatsReportsReady(RTCStatsReportsReadyEvent evt)
        {
            var reports = evt.rtcStatsReports;
            Task.Run(() => ProcessReports(reports));
        }

        private void ProcessReports(IList<RTCStatsReport> reports)
        {
            foreach (var report in reports)
            {
                if (report.StatsType == RTCStatsType.StatsReportTypeSsrc)
                {
                    var statValues = report.Values;
                    if (statValues.Keys.Contains(RTCStatsValueName.StatsValueNameTrackId))
                    {
                        var trackId = statValues[RTCStatsValueName.StatsValueNameTrackId].ToString();
                        if (trackId.StartsWith("audio_label"))
                        {
                            if (statValues.Keys.Contains(RTCStatsValueName.StatsValueNamePacketsSent))
                            {
                                _metricsCollector.AudioPacketsSent +=
                                    Convert.ToInt32(statValues[RTCStatsValueName.StatsValueNamePacketsSent]);
                            }
                            if (statValues.Keys.Contains(RTCStatsValueName.StatsValueNamePacketsLost))
                            {
                                _metricsCollector.AudioPacketsLost +=
                                    Convert.ToInt32(statValues[RTCStatsValueName.StatsValueNamePacketsLost]);
                            }
                            if (statValues.Keys.Contains(RTCStatsValueName.StatsValueNameCurrentDelayMs))
                            {
                                _metricsCollector.AudioCurrentDelayMs +=
                                    Convert.ToDouble(statValues[RTCStatsValueName.StatsValueNameCurrentDelayMs]);
                                _metricsCollector.AudioDelayCount++;
                            }
                            if (statValues.Keys.Contains(RTCStatsValueName.StatsValueNameCodecName))
                            {
                                _metricsCollector.AudioCodec =
                                    statValues[RTCStatsValueName.StatsValueNameCodecName].ToString();
                            }
                        }
                        else if (trackId.StartsWith("video_label"))
                        {
                            if (statValues.Keys.Contains(RTCStatsValueName.StatsValueNamePacketsSent))
                            {
                                _metricsCollector.VideoPacketsSent +=
                                    Convert.ToInt32(statValues[RTCStatsValueName.StatsValueNamePacketsSent]);
                            }
                            if (statValues.Keys.Contains(RTCStatsValueName.StatsValueNamePacketsLost))
                            {
                                _metricsCollector.VideoPacketsLost +=
                                    Convert.ToInt32(statValues[RTCStatsValueName.StatsValueNamePacketsLost]);
                            }
                            if (statValues.Keys.Contains(RTCStatsValueName.StatsValueNameCurrentDelayMs))
                            {
                                _metricsCollector.VideoCurrentDelayMs +=
                                    Convert.ToDouble(statValues[RTCStatsValueName.StatsValueNameCurrentDelayMs]);
                                _metricsCollector.VideoDelayCount++;
                            }
                            if (statValues.Keys.Contains(RTCStatsValueName.StatsValueNameFrameHeightSent))
                            {
                                _metricsCollector.FrameHeight =
                                    Convert.ToInt32(statValues[RTCStatsValueName.StatsValueNameFrameHeightSent]);
                            }
                            if (statValues.Keys.Contains(RTCStatsValueName.StatsValueNameFrameWidthSent))
                            {
                                _metricsCollector.FrameWidth =
                                    Convert.ToInt32(statValues[RTCStatsValueName.StatsValueNameFrameWidthSent]);
                            }
                            if (statValues.Keys.Contains(RTCStatsValueName.StatsValueNameCodecName))
                            {
                                _metricsCollector.VideoCodec =
                                    statValues[RTCStatsValueName.StatsValueNameCodecName].ToString();
                            }
                        }
                    }
                }
            }
        }

        private string ToMetricName(RTCStatsValueName name)
        {
            switch (name)
            {
                case RTCStatsValueName.StatsValueNameAudioOutputLevel:
                    return "audioOutputLevel";
                case RTCStatsValueName.StatsValueNameAudioInputLevel:
                    return "audioInputLevel";
                case RTCStatsValueName.StatsValueNameBytesSent:
                    return "bytesSent";
                case RTCStatsValueName.StatsValueNamePacketsSent:
                    return "packetsSent";
                case RTCStatsValueName.StatsValueNameBytesReceived:
                    return "bytesReceived";
                case RTCStatsValueName.StatsValueNameLabel:
                    return "label";
                case RTCStatsValueName.StatsValueNamePacketsReceived:
                    return "packetsReceived";
                case RTCStatsValueName.StatsValueNamePacketsLost:
                    return "packetsLost";
                case RTCStatsValueName.StatsValueNameProtocol:
                    return "protocol";
                case RTCStatsValueName.StatsValueNameTransportId:
                    return "transportId";
                case RTCStatsValueName.StatsValueNameSelectedCandidatePairId:
                    return "selectedCandidatePairId";
                case RTCStatsValueName.StatsValueNameSsrc:
                    return "ssrc";
                case RTCStatsValueName.StatsValueNameState:
                    return "state";
                case RTCStatsValueName.StatsValueNameDataChannelId:
                    return "datachannelid";

                // 'goog' prefixed constants.
                case RTCStatsValueName.StatsValueNameAccelerateRate:
                    return "googAccelerateRate";
                case RTCStatsValueName.StatsValueNameActiveConnection:
                    return "googActiveConnection";
                case RTCStatsValueName.StatsValueNameActualEncBitrate:
                    return "googActualEncBitrate";
                case RTCStatsValueName.StatsValueNameAvailableReceiveBandwidth:
                    return "googAvailableReceiveBandwidth";
                case RTCStatsValueName.StatsValueNameAvailableSendBandwidth:
                    return "googAvailableSendBandwidth";
                case RTCStatsValueName.StatsValueNameAvgEncodeMs:
                    return "googAvgEncodeMs";
                case RTCStatsValueName.StatsValueNameBucketDelay:
                    return "googBucketDelay";
                case RTCStatsValueName.StatsValueNameBandwidthLimitedResolution:
                    return "googBandwidthLimitedResolution";

                // Candidate related attributes. Values are taken from
                // http://w3c.github.io/webrtc-stats/#rtcstatstype-enum*.
                case RTCStatsValueName.StatsValueNameCandidateIPAddress:
                    return "ipAddress";
                case RTCStatsValueName.StatsValueNameCandidateNetworkType:
                    return "networkType";
                case RTCStatsValueName.StatsValueNameCandidatePortNumber:
                    return "portNumber";
                case RTCStatsValueName.StatsValueNameCandidatePriority:
                    return "priority";
                case RTCStatsValueName.StatsValueNameCandidateTransportType:
                    return "transport";
                case RTCStatsValueName.StatsValueNameCandidateType:
                    return "candidateType";

                case RTCStatsValueName.StatsValueNameChannelId:
                    return "googChannelId";
                case RTCStatsValueName.StatsValueNameCodecName:
                    return "googCodecName";
                case RTCStatsValueName.StatsValueNameComponent:
                    return "googComponent";
                case RTCStatsValueName.StatsValueNameContentName:
                    return "googContentName";
                case RTCStatsValueName.StatsValueNameCpuLimitedResolution:
                    return "googCpuLimitedResolution";
                case RTCStatsValueName.StatsValueNameDecodingCTSG:
                    return "googDecodingCTSG";
                case RTCStatsValueName.StatsValueNameDecodingCTN:
                    return "googDecodingCTN";
                case RTCStatsValueName.StatsValueNameDecodingNormal:
                    return "googDecodingNormal";
                case RTCStatsValueName.StatsValueNameDecodingPLC:
                    return "googDecodingPLC";
                case RTCStatsValueName.StatsValueNameDecodingCNG:
                    return "googDecodingCNG";
                case RTCStatsValueName.StatsValueNameDecodingPLCCNG:
                    return "googDecodingPLCCNG";
                case RTCStatsValueName.StatsValueNameDer:
                    return "googDerBase64";
                case RTCStatsValueName.StatsValueNameDtlsCipher:
                    return "dtlsCipher";
                case RTCStatsValueName.StatsValueNameEchoCancellationQualityMin:
                    return "googEchoCancellationQualityMin";
                case RTCStatsValueName.StatsValueNameEchoDelayMedian:
                    return "googEchoCancellationEchoDelayMedian";
                case RTCStatsValueName.StatsValueNameEchoDelayStdDev:
                    return "googEchoCancellationEchoDelayStdDev";
                case RTCStatsValueName.StatsValueNameEchoReturnLoss:
                    return "googEchoCancellationReturnLoss";
                case RTCStatsValueName.StatsValueNameEchoReturnLossEnhancement:
                    return "googEchoCancellationReturnLossEnhancement";
                case RTCStatsValueName.StatsValueNameEncodeUsagePercent:
                    return "googEncodeUsagePercent";
                case RTCStatsValueName.StatsValueNameExpandRate:
                    return "googExpandRate";
                case RTCStatsValueName.StatsValueNameFingerprint:
                    return "googFingerprint";
                case RTCStatsValueName.StatsValueNameFingerprintAlgorithm:
                    return "googFingerprintAlgorithm";
                case RTCStatsValueName.StatsValueNameFirsReceived:
                    return "googFirsReceived";
                case RTCStatsValueName.StatsValueNameFirsSent:
                    return "googFirsSent";
                case RTCStatsValueName.StatsValueNameFrameHeightInput:
                    return "googFrameHeightInput";
                case RTCStatsValueName.StatsValueNameFrameHeightReceived:
                    return "googFrameHeightReceived";
                case RTCStatsValueName.StatsValueNameFrameHeightSent:
                    return "googFrameHeightSent";
                case RTCStatsValueName.StatsValueNameFrameRateReceived:
                    return "googFrameRateReceived";
                case RTCStatsValueName.StatsValueNameFrameRateDecoded:
                    return "googFrameRateDecoded";
                case RTCStatsValueName.StatsValueNameFrameRateOutput:
                    return "googFrameRateOutput";
                case RTCStatsValueName.StatsValueNameDecodeMs:
                    return "googDecodeMs";
                case RTCStatsValueName.StatsValueNameMaxDecodeMs:
                    return "googMaxDecodeMs";
                case RTCStatsValueName.StatsValueNameCurrentDelayMs:
                    return "googCurrentDelayMs";
                case RTCStatsValueName.StatsValueNameTargetDelayMs:
                    return "googTargetDelayMs";
                case RTCStatsValueName.StatsValueNameJitterBufferMs:
                    return "googJitterBufferMs";
                case RTCStatsValueName.StatsValueNameMinPlayoutDelayMs:
                    return "googMinPlayoutDelayMs";
                case RTCStatsValueName.StatsValueNameRenderDelayMs:
                    return "googRenderDelayMs";
                case RTCStatsValueName.StatsValueNameCaptureStartNtpTimeMs:
                    return "googCaptureStartNtpTimeMs";
                case RTCStatsValueName.StatsValueNameFrameRateInput:
                    return "googFrameRateInput";
                case RTCStatsValueName.StatsValueNameFrameRateSent:
                    return "googFrameRateSent";
                case RTCStatsValueName.StatsValueNameFrameWidthInput:
                    return "googFrameWidthInput";
                case RTCStatsValueName.StatsValueNameFrameWidthReceived:
                    return "googFrameWidthReceived";
                case RTCStatsValueName.StatsValueNameFrameWidthSent:
                    return "googFrameWidthSent";
                case RTCStatsValueName.StatsValueNameInitiator:
                    return "googInitiator";
                case RTCStatsValueName.StatsValueNameIssuerId:
                    return "googIssuerId";
                case RTCStatsValueName.StatsValueNameJitterReceived:
                    return "googJitterReceived";
                case RTCStatsValueName.StatsValueNameLocalAddress:
                    return "googLocalAddress";
                case RTCStatsValueName.StatsValueNameLocalCandidateId:
                    return "localCandidateId";
                case RTCStatsValueName.StatsValueNameLocalCandidateType:
                    return "googLocalCandidateType";
                case RTCStatsValueName.StatsValueNameLocalCertificateId:
                    return "localCertificateId";
                case RTCStatsValueName.StatsValueNameAdaptationChanges:
                    return "googAdaptationChanges";
                case RTCStatsValueName.StatsValueNameNacksReceived:
                    return "googNacksReceived";
                case RTCStatsValueName.StatsValueNameNacksSent:
                    return "googNacksSent";
                case RTCStatsValueName.StatsValueNamePreemptiveExpandRate:
                    return "googPreemptiveExpandRate";
                case RTCStatsValueName.StatsValueNamePlisReceived:
                    return "googPlisReceived";
                case RTCStatsValueName.StatsValueNamePlisSent:
                    return "googPlisSent";
                case RTCStatsValueName.StatsValueNamePreferredJitterBufferMs:
                    return "googPreferredJitterBufferMs";
                case RTCStatsValueName.StatsValueNameRemoteAddress:
                    return "googRemoteAddress";
                case RTCStatsValueName.StatsValueNameRemoteCandidateId:
                    return "remoteCandidateId";
                case RTCStatsValueName.StatsValueNameRemoteCandidateType:
                    return "googRemoteCandidateType";
                case RTCStatsValueName.StatsValueNameRemoteCertificateId:
                    return "remoteCertificateId";
                case RTCStatsValueName.StatsValueNameRetransmitBitrate:
                    return "googRetransmitBitrate";
                case RTCStatsValueName.StatsValueNameRtt:
                    return "googRtt";
                case RTCStatsValueName.StatsValueNameSecondaryDecodedRate:
                    return "googSecondaryDecodedRate";
                case RTCStatsValueName.StatsValueNameSendPacketsDiscarded:
                    return "packetsDiscardedOnSend";
                case RTCStatsValueName.StatsValueNameSpeechExpandRate:
                    return "googSpeechExpandRate";
                case RTCStatsValueName.StatsValueNameSrtpCipher:
                    return "srtpCipher";
                case RTCStatsValueName.StatsValueNameTargetEncBitrate:
                    return "googTargetEncBitrate";
                case RTCStatsValueName.StatsValueNameTransmitBitrate:
                    return "googTransmitBitrate";
                case RTCStatsValueName.StatsValueNameTransportType:
                    return "googTransportType";
                case RTCStatsValueName.StatsValueNameTrackId:
                    return "googTrackId";
                case RTCStatsValueName.StatsValueNameTypingNoiseState:
                    return "googTypingNoiseState";
                case RTCStatsValueName.StatsValueNameViewLimitedResolution:
                    return "googViewLimitedResolution";
                case RTCStatsValueName.StatsValueNameWritable:
                    return "googWritable";
                case RTCStatsValueName.StatsValueNameCurrentEndToEndDelayMs:
                    return "currentEndToEndDelayMs";
                default:
                    return string.Empty;
            }
        }

        ~StatsManager()
        {
            _telemetry.Flush();
        }
    }
}