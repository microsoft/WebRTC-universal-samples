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
using System.Threading.Tasks;
using Windows.Networking.Connectivity;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace ChatterBox.Background
{
    internal class AudioVideoMetricsCollector
    {
        private readonly TelemetryClient _telemetry;
        private string _audioCodec;

        private int _frameHeight;

        private int _frameWidth;
        private int _inboundMaxBitsPerSecondCount;
        private ulong _inboundMaxBitsPerSecondSum;
        private int _outboundMaxBitsPerSecondCount;
        private ulong _outboundMaxBitsPerSecondSum;

        private string _videoCodec;

        public AudioVideoMetricsCollector(TelemetryClient tc)
        {
            _telemetry = tc;
            ResetPackets();
            ResetDelays();
            _frameHeight = 0;
            _frameWidth = 0;
            _inboundMaxBitsPerSecondSum = 0;
            _inboundMaxBitsPerSecondCount = 0;
            _outboundMaxBitsPerSecondSum = 0;
            _outboundMaxBitsPerSecondCount = 0;
        }

        public string AudioCodec
        {
            get { return _audioCodec; }
            set
            {
                if (_audioCodec != value)
                {
                    _audioCodec = value;
                    TrackCodecUseForCall(value, "Audio");
                }
            }
        }

        public double AudioCurrentDelayMs { get; set; }
        public int AudioDelayCount { get; set; }
        public int AudioPacketsLost { get; set; }
        public int AudioPacketsSent { get; set; }

        public int FrameHeight
        {
            get { return _frameHeight; }
            set
            {
                if (_frameHeight > value)
                {
                    TrackVideoResolutionDowngrade(_frameHeight, value, "Height");
                }
                _frameHeight = value;
            }
        }

        public int FrameWidth
        {
            get { return _frameWidth; }
            set
            {
                if (_frameWidth > value)
                {
                    TrackVideoResolutionDowngrade(_frameWidth, value, "Width");
                }
                _frameWidth = value;
            }
        }

        public string VideoCodec
        {
            get { return _videoCodec; }
            set
            {
                if (_videoCodec != value)
                {
                    _videoCodec = value;
                    TrackCodecUseForCall(value, "Video");
                }
            }
        }

        public double VideoCurrentDelayMs { get; set; }
        public int VideoDelayCount { get; set; }
        public int VideoPacketsLost { get; set; }
        public int VideoPacketsSent { get; set; }

        public void CollectNewtorkMetrics(object state)
        {
            var networkAdapter = NetworkInformation.GetInternetConnectionProfile().NetworkAdapter;
            _inboundMaxBitsPerSecondSum += networkAdapter.InboundMaxBitsPerSecond;
            _outboundMaxBitsPerSecondSum += networkAdapter.OutboundMaxBitsPerSecond;
            _inboundMaxBitsPerSecondCount++;
            _outboundMaxBitsPerSecondCount++;
        }

        public void TrackCurrentDelayMetrics()
        {
            if (AudioDelayCount > 0)
            {
                var metric = new MetricTelemetry("Audio Current Delay Ratio", AudioCurrentDelayMs/AudioDelayCount);
                metric.Timestamp = DateTimeOffset.UtcNow;
                Task.Run(() => _telemetry.TrackMetric(metric));
            }
            if (VideoDelayCount > 0)
            {
                var metric = new MetricTelemetry("Video Current Delay Ratio", VideoCurrentDelayMs/VideoDelayCount);
                metric.Timestamp = DateTimeOffset.UtcNow;
                Task.Run(() => _telemetry.TrackMetric(metric));
            }
            ResetDelays();
        }

        public void TrackMetrics(object state)
        {
            var audioPacketRatio = AudioPacketsSent != 0 ? (double) AudioPacketsLost/AudioPacketsSent : 0;
            var videoPacketRatio = VideoPacketsSent != 0 ? (double) VideoPacketsLost/VideoPacketsSent : 0;

            if (AudioPacketsSent > 0)
            {
                var metric = new MetricTelemetry("Audio Packet Lost Ratio", audioPacketRatio);
                metric.Timestamp = DateTimeOffset.UtcNow;
                Task.Run(() => _telemetry.TrackMetric(metric));
            }

            if (VideoPacketsSent > 0)
            {
                var metric = new MetricTelemetry("Video Packet Lost Ratio", videoPacketRatio);
                metric.Timestamp = DateTimeOffset.UtcNow;
                Task.Run(() => _telemetry.TrackMetric(metric));
            }

            // reset flags for the new time period
            ResetPackets();
        }

        public void TrackNetworkQualityMetrics()
        {
            IDictionary<string, string> properties = new Dictionary<string, string>
            {
                {"Timestamp", DateTimeOffset.UtcNow.ToString(@"hh\:mm\:ss")}
            };
            IDictionary<string, double> metrics = new Dictionary<string, double>();

            if (_inboundMaxBitsPerSecondCount != 0)
            {
                metrics.Add("Maximum Inbound Speed (bit/sec)",
                    (double) _inboundMaxBitsPerSecondSum/_inboundMaxBitsPerSecondCount);
            }
            if (_outboundMaxBitsPerSecondCount != 0)
            {
                metrics.Add("Maximum Outbound Speed (bit/sec)",
                    (double) _outboundMaxBitsPerSecondSum/_outboundMaxBitsPerSecondCount);
            }
            Task.Run(() => _telemetry.TrackEvent("Network Avarage Quality During Call", properties, metrics));
            _inboundMaxBitsPerSecondSum = 0;
            _inboundMaxBitsPerSecondCount = 0;
            _outboundMaxBitsPerSecondSum = 0;
            _outboundMaxBitsPerSecondCount = 0;
        }

        private void ResetDelays()
        {
            AudioCurrentDelayMs = 0;
            AudioDelayCount = 0;
            VideoCurrentDelayMs = 0;
            VideoDelayCount = 0;
        }

        private void ResetPackets()
        {
            AudioPacketsSent = 0;
            AudioPacketsLost = 0;
            VideoPacketsSent = 0;
            VideoPacketsLost = 0;
        }

        private void TrackCodecUseForCall(string codecValue, string codecType)
        {
            IDictionary<string, string> properties = new Dictionary<string, string>
            {
                {"Timestamp", DateTimeOffset.UtcNow.ToString(@"hh\:mm\:ss")},
                {codecType + " codec used for call", codecValue}
            };
            Task.Run(() => _telemetry.TrackEvent(codecType + " codec", properties));
        }

        private void TrackVideoResolutionDowngrade(int oldValue, int newValue, string name)
        {
            IDictionary<string, string> properties = new Dictionary<string, string>
            {
                {"Timestamp", DateTimeOffset.UtcNow.ToString(@"hh\:mm\:ss")}
            };
            IDictionary<string, double> metrics = new Dictionary<string, double>
            {
                {"Old " + name, oldValue},
                {"New " + name, newValue}
            };
            Task.Run(() => _telemetry.TrackEvent("Video " + name + " Downgrade", properties, metrics));
        }
    }
}