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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Networking.Connectivity;
using Windows.Storage;
using Windows.Storage.AccessCache;
using ChatterBox.Background.AppService.Dto;
using ChatterBox.Background.Call.States;
using ChatterBox.Background.Call.States.Interfaces;
using ChatterBox.Background.Call.Utils;
using ChatterBox.Background.Notifications;
using ChatterBox.Background.Settings;
using ChatterBox.Communication.Messages.Relay;
using Org.WebRtc;
using ChatterBoxClient.Universal.BackgroundRenderer;

#pragma warning disable 4014

namespace ChatterBox.Background.Call
{
    internal class CallContext
    {
        public const string LocalMediaStreamId = "LOCAL";
        public const string PeerMediaStreamId = "PEER";
        private readonly IHub _hub;
        private readonly SemaphoreSlim _iceBufferSemaphore = new SemaphoreSlim(1, 1);


        // Semaphore used to make sure only one call
        // is executed at any given time.
        private readonly SemaphoreSlim _sem = new SemaphoreSlim(1, 1);
        private List<RTCIceCandidate> _bufferedIceCandidates = new List<RTCIceCandidate>();

        private DateTimeOffset _callStartDateTime;

        private uint _foregroundProcessId;

        private Timer _iceCandidateBufferTimer;

        private bool _isVideoEnabled;

        private MediaStream _localStream;

        private bool _microphoneMuted;

        private bool _streamEventsRegistered;

        public CallContext(IHub hub,
            IVoipHelper voipHelper)
        {
            _hub = hub;
            VoipHelper = voipHelper;

            var idleState = new Idle();
            SwitchState(idleState).Wait();

            Hub.Instance.MediaSettingsChannel.OnTracingStatisticsChanged += statsEnabled =>
            {
                if (_peerConnection != null)
                {
                    _peerConnection.EtwStatsEnabled = statsEnabled;
                }
            };

            ResetRenderers();
        }

        private RTCPeerConnection _peerConnection { get; set; }

        public uint ForegroundProcessId
        {
            get
            {
                lock (this)
                {
                    return _foregroundProcessId;
                }
            }
            set
            {
                lock (this)
                {
                    _foregroundProcessId = value;
                }
            }
        }

        public bool IsVideoEnabled
        {
            get
            {
                lock (this)
                {
                    return _isVideoEnabled;
                }
            }
            set
            {
                var triggerStat = false;
                lock (this)
                {
                    if(_isVideoEnabled != value)
                    {
                        triggerStat = true;
                    }

                    _isVideoEnabled = value;
                    ApplyVideoConfig();
                }
                if(triggerStat)
                {
                    var operation = value ? "Video On" : "Video Off";
                    ETWEventLogger.Instance.LogEvent(operation, DateTimeOffset.Now.ToUnixTimeMilliseconds());
                }
            }
        }

        public bool MicrophoneMuted
        {
            get
            {
                lock (this)
                {
                    return _microphoneMuted;
                }
            }
            set
            {
                var triggerStat = false;
                lock (this)
                {
                    if (_microphoneMuted != value)
                    {
                        triggerStat = true;
                    }
                    _microphoneMuted = value;
                    ApplyMicrophoneConfig();
                }
                if (triggerStat)
                {
                    var operation = value ? "Muted" : "UnMuted";
                    ETWEventLogger.Instance.LogEvent("Call " + operation, DateTimeOffset.Now.ToUnixTimeMilliseconds());
                }
            }
        }

        public RTCPeerConnection PeerConnection
        {
            get { return _peerConnection; }
            set
            {
                _peerConnection = value;
                if (_peerConnection != null)
                {
                    // Register to the events from the peer connection.
                    // We'll forward them to the state.
                    _peerConnection.OnIceCandidate += evt =>
                    {
                        if (evt.Candidate != null)
                        {
                            var task = QueueIceCandidate(evt.Candidate);
                        }
                    };

                    if (_hub.IsAppInsightsEnabled)
                    {
                        _hub.InitialiazeStatsManager(_peerConnection);
                        _hub.ToggleStatsManagerConnectionState(true);
                    }

                    _peerConnection.EtwStatsEnabled = Hub.Instance.MediaSettingsChannel.EtwStatsEnabled;

                    if (Hub.Instance.MediaSettingsChannel.StatsConfig != null)
                    {
                        if (Hub.Instance.MediaSettingsChannel.StatsConfig.SendStatsToServerEnabled)
                        {
                            _peerConnection.RtcStatsDestinationHost = Hub.Instance.MediaSettingsChannel.StatsConfig.StatsServerHost;
                            _peerConnection.RtcStatsDestinationPort = Hub.Instance.MediaSettingsChannel.StatsConfig.StatsServerPort;
                        }
                        _peerConnection.SendRtcStatsToRemoteHostEnabled = Hub.Instance.MediaSettingsChannel.StatsConfig.SendStatsToServerEnabled;
                    }
                    else
                    {
                        _peerConnection.SendRtcStatsToRemoteHostEnabled = false;
                    }

                    _peerConnection.OnAddStream += evt =>
                    {
                        if (evt.Stream != null)
                        {
                            Task.Run(async () => { await WithState(async st => await st.OnAddStreamAsync(evt.Stream)); });
                        }
                    };
                }
                else
                {
                    if (_hub.IsAppInsightsEnabled)
                    {
                        _hub.ToggleStatsManagerConnectionState(false);
                    }
                }
            }
        }

        public string PeerId { get; set; }
        public string PeerName { get; set; }

        public MediaStream LocalStream
        {
            get { return _localStream; }
            set
            {
                _localStream = value;
                ApplyMicrophoneConfig();
                ApplyVideoConfig();
            }
        }
        public Size LocalVideoControlSize { get; set; }
        public Renderer LocalVideoRenderer { get; private set; }

        public MediaStream RemoteStream { get; set; }
        public Org.WebRtc.CodecInfo VideoCodecUsed { get; set; }
        public Size RemoteVideoControlSize { get; set; }
        public Renderer RemoteVideoRenderer { get; private set; }

        internal CallType CallType { get; set; }
        private BaseCallState State { get; set; }

        public IVoipHelper VoipHelper { get; set; }

        public MediaSettingsChannel MediaSettingsChannel => Hub.Instance.MediaSettingsChannel;

        /// <summary>
        ///     WebRTC initialization has to be done when we have access to the
        ///     resources. That's inside an active voip call.
        ///     This function must be called after VoipHelper.StartVoipTask()
        /// </summary>
        /// <returns></returns>
        public void InitializeRTC()
        {
            RtcManager.Instance.EnsureRtcIsInitialized();
            if (!_streamEventsRegistered)
            {
                ResolutionHelper.ResolutionChanged += (id, width, height) =>
                {
                    if (id == LocalMediaStreamId)
                    {
                        ETWEventLogger.Instance.LogEvent("Local Video Resolution Changed",
                            "width = " + width + " height = " + height,
                            DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString());
                    }
                    else if (id == PeerMediaStreamId)
                    {
                        ETWEventLogger.Instance.LogEvent("Remote Video Resolution Changed",
                            "width = " + width + " height = " + height,
                            DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString());
                    }
                };

                FrameCounterHelper.FramesPerSecondChanged += (id, frameRate) =>
                {
                    if (id == LocalMediaStreamId)
                    {
                        LocalVideo_FrameRateUpdate(frameRate);
                    }
                    else if (id == PeerMediaStreamId)
                    {
                        RemoteVideo_FrameRateUpdate(frameRate);
                    }
                };
                FirstFrameRenderHelper.FirstFrameRendered += (timestamp) =>
                {
                    ETWEventLogger.Instance.LogEvent("First Frame Rendered", (long)timestamp);
                };

                _streamEventsRegistered = true;
            }
        }

        public void ResetRenderers()
        {
            ResetLocalRenderer();
            ResetRemoteRenderer();
        }

        public void ResetLocalRenderer()
        {
            if (LocalVideoRenderer != null)
            {
                LocalVideoRenderer.Teardown();
                LocalVideoRenderer = null;
            }
            LocalVideoRenderer = null;
            GC.Collect();
            LocalVideoRenderer = new Renderer();
            LocalVideoRenderer.RenderFormatUpdate += LocalVideoRenderer_RenderFormatUpdate;
        }

        public void ResetRemoteRenderer()
        {
            if (RemoteVideoRenderer != null)
            {
                RemoteVideoRenderer.Teardown();
                RemoteVideoRenderer = null;
            }
            RemoteVideoRenderer = null;
            GC.Collect();
            RemoteVideoRenderer = new Renderer();
            RemoteVideoRenderer.RenderFormatUpdate += RemoteVideoRenderer_RenderFormatUpdate;
        }

        public void SaveWebRTCTrace()
        {
            if (!WebRTC.IsTracing())
            {
                return;
            }

            Task.Run(async () =>
            {
                WebRTC.StopTracing(); //stop tracing so that trace file can be properly saved.
                ToastNotificationService.ShowToastNotification("Saving Webrtc trace");
                var webrtcTraceInternalFile = ApplicationData.Current.LocalFolder.Path + "\\" + "_webrtc_trace.txt";

                WebRTC.SaveTrace(webrtcTraceInternalFile);

                await SaveToUserPickedFolder(webrtcTraceInternalFile);

                WebRTC.StartTracing();

                ToastNotificationService.ShowToastNotification("Saving Webrtc trace finished!");
            });
        }

        public void SendToPeer(string tag, string payload)
        {
            if (PeerId == null)
                return;
            _hub.Relay(new RelayMessage
            {
                FromUserId = RegistrationSettings.UserId,
                ToUserId = PeerId,
                Tag = tag,
                Payload = payload
            });
        }

        public async Task SwitchState(BaseCallState newState)
        {
            Debug.WriteLine($"CallContext.SwitchState {State?.GetType().Name} -> {newState.GetType().Name}");
            if (State != null)
            {
                await State.LeaveStateAsync();
            }
            State = newState;
            await State.EnterStateAsync(this);

            Task.Run(() => _hub.OnCallStatusAsync(GetCallStatus()));
        }

        public void TrackCallEnded()
        {
            if (!_hub.IsAppInsightsEnabled)
            {
                return;
            }
            // log call duration as CallEnded event property
            var duration = DateTimeOffset.Now.Subtract(_callStartDateTime).Duration().ToString(@"hh\:mm\:ss");
            var properties = new Dictionary<string, string> {{"CallAsync Duration", duration}};
            _hub.TrackStatsManagerEvent("CallEnded", properties);

            // stop call watch, so the duration will be calculated and tracked as request
            _hub.StopStatsManagerCallWatch();
        }

        public void TrackCallStarted()
        {
            _hub.IsAppInsightsEnabled = SignalingSettings.AppInsightsEnabled;
            if (!_hub.IsAppInsightsEnabled)
            {
                return;
            }
            _callStartDateTime = DateTimeOffset.Now;
            var currentConnection = NetworkInformation.GetInternetConnectionProfile();
            string connType;
            switch (currentConnection.NetworkAdapter.IanaInterfaceType)
            {
                case 6:
                    connType = "Cable";
                    break;
                case 71:
                    connType = "WiFi";
                    break;
                case 243:
                    connType = "Mobile";
                    break;
                default:
                    connType = "Unknown";
                    break;
            }
            var properties = new Dictionary<string, string> {{"Connection Type", connType}};
            _hub.TrackStatsManagerEvent("CallStarted", properties);
            // start call watch to count duration for tracking as request
            _hub.StartStatsManagerCallWatch();
        }

        public async Task WithContextAction(Action<CallContext> fn)
        {
            using (var autoLock = new AutoLock(_sem))
            {
                await autoLock.WaitAsync();

                try
                {
                    fn(this);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    if (Debugger.IsAttached)
                        throw;
                }
            }
        }

        public async Task WithContextActionAsync(Func<CallContext, Task> fn)
        {
            using (var autoLock = new AutoLock(_sem))
            {
                await autoLock.WaitAsync();

                try
                {
                    await fn(this);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    if (Debugger.IsAttached)
                        throw;
                }
            }
        }

        public async Task<TResult> WithContextFunc<TResult>(Func<CallContext, TResult> fn)
        {
            using (var autoLock = new AutoLock(_sem))
            {
                await autoLock.WaitAsync();

                try
                {
                    return fn(this);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    if (Debugger.IsAttached)
                        throw;
                }
            }
            return default(TResult);
        }

        public async Task<TResult> WithContextFuncAsync<TResult>(Func<CallContext, Task<TResult>> fn)
        {
            using (var autoLock = new AutoLock(_sem))
            {
                await autoLock.WaitAsync();

                try
                {
                    return await fn(this);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    if (Debugger.IsAttached)
                        throw;
                }
            }
            return default(TResult);
        }

        public async Task WithState(Func<BaseCallState, Task> fn)
        {
            using (var autoLock = new AutoLock(_sem))
            {
                await autoLock.WaitAsync();

                try
                {
                    await fn(State);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    if (Debugger.IsAttached)
                        throw;
                }
            }
        }

        internal CallStatus GetCallStatus()
        {
            return new CallStatus
            {
                PeerId = PeerId,
                HasPeerConnection = PeerConnection != null,
                State = State.CallState,
                IsVideoEnabled = IsVideoEnabled,
                Type = CallType
            };
        }

        internal FrameFormat GetFrameFormat(bool local)
        {
            var videoRenderer = local ? LocalVideoRenderer : RemoteVideoRenderer;
            if (videoRenderer != null && videoRenderer.IsInitialized)
            {
                Int64 swapChainHandle = 0;
                UInt32 width = 0, height = 0;
                UInt32 foregroundProcessId = 0;
                if(videoRenderer.GetRenderFormat(out swapChainHandle, out width, out height, out foregroundProcessId))
                {
                    return new FrameFormat
                    {
                        IsLocal = local,
                        SwapChainHandle = swapChainHandle,
                        Width = width,
                        Height = height,
                        ForegroundProcessId = foregroundProcessId
                    };
                }
            }
            return null;
        }

        private void ApplyMicrophoneConfig()
        {
            if (LocalStream != null)
            {
                foreach (var audioTrack in LocalStream.GetAudioTracks())
                {
                    audioTrack.Enabled = !_microphoneMuted;
                }
            }
        }

        private void ApplyVideoConfig()
        {
            if (LocalStream != null)
            {
                foreach (var videoTrack in LocalStream.GetVideoTracks())
                {
                    videoTrack.Enabled = _isVideoEnabled;
                }
            }
        }

        private async void FlushBufferedIceCandidatesEventHandler(object state)
        {
            using (var autoLock = new AutoLock(_iceBufferSemaphore))
            {
                await autoLock.WaitAsync();
                _iceCandidateBufferTimer = null;

                // Chunk in groups of 10 to not blow the size limit
                // on the storage used by the receiving side.
                while (_bufferedIceCandidates.Count > 0)
                {
                    var candidates = _bufferedIceCandidates.Take(10).ToArray();
                    _bufferedIceCandidates = _bufferedIceCandidates.Skip(10).ToList();
                    await WithState(async st => await st.SendLocalIceCandidatesAsync(candidates));
                }
            }
        }

        private void LocalVideo_FrameRateUpdate(string fpsValue)
        {
            _hub.OnUpdateFrameRate(
                new FrameRate
                {
                    IsLocal = true,
                    Fps = fpsValue
                });
        }

        private void LocalVideoRenderer_RenderFormatUpdate(long swapChainHandle, uint width, uint height,
            uint foregroundProcessId)
        {
            _hub.OnUpdateFrameFormat(
                new FrameFormat
                {
                    IsLocal = true,
                    SwapChainHandle = swapChainHandle,
                    Width = width,
                    Height = height,
                    ForegroundProcessId = foregroundProcessId
                });
        }

        private void RemoteVideoRenderer_RenderFormatUpdate(long swapChainHandle, uint width, uint height,
            uint foregroundProcessId)
        {
            _hub.OnUpdateFrameFormat(
                new FrameFormat
                {
                    IsLocal = false,
                    SwapChainHandle = swapChainHandle,
                    Width = width,
                    Height = height,
                    ForegroundProcessId = foregroundProcessId
                });
        }

        private async Task QueueIceCandidate(RTCIceCandidate candidate)
        {
            using (var autoLock = new AutoLock(_iceBufferSemaphore))
            {
                await autoLock.WaitAsync();
                _bufferedIceCandidates.Add(candidate);
                if (_iceCandidateBufferTimer == null)
                {
                    // Flush the ice candidates in 100ms.
                    _iceCandidateBufferTimer = new Timer(FlushBufferedIceCandidatesEventHandler, null, 100,
                        Timeout.Infinite);
                }
            }
        }

        private void RemoteVideo_FrameRateUpdate(string fpsValue)
        {
            _hub.OnUpdateFrameRate(
                new FrameRate
                {
                    IsLocal = false,
                    Fps = fpsValue
                });
        }


        private async Task SaveToUserPickedFolder(string sourceFile)
        {
            var token = Hub.Instance.MediaSettingsChannel.GetTraceFolderToken();
            var folder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(token);
            //default capture resolution is 640x480 if user did not set
            var width = 640;
            var height = 480;
            var settings = ApplicationData.Current.LocalSettings;
            if (settings.Values.ContainsKey(MediaSettingsIds.PreferredVideoCaptureWidth) &&
                settings.Values.ContainsKey(MediaSettingsIds.PreferredVideoCaptureHeight))
            {
                width = (int) settings.Values[MediaSettingsIds.PreferredVideoCaptureWidth];
                height = (int) settings.Values[MediaSettingsIds.PreferredVideoCaptureHeight];
            }
            var now = DateTime.Now;

            var videoCodec = await Hub.Instance.MediaSettingsChannel.GetVideoCodecAsync();
            var targetFileName =
                $"webrtc_trace_{videoCodec.Name}_{RegistrationSettings.Name}_{PeerName}_{width}X{height}";
            targetFileName += $"_{now.Month}{now.Day}{now.Hour}{now.Minute}{now.Second}.txt";
            var targetFile = await folder.CreateFileAsync(targetFileName);
            var source = await StorageFile.GetFileFromPathAsync(sourceFile);

            await source.CopyAndReplaceAsync(targetFile);
        }
    }
}