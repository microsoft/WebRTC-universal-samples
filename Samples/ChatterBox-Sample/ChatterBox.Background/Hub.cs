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
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using ChatterBox.Background.AppService;
using ChatterBox.Background.AppService.Dto;
using ChatterBox.Background.Call;
using ChatterBox.Background.Helpers;
using ChatterBox.Background.Settings;
using ChatterBox.Background.Signaling;
using ChatterBox.Background.Tasks;
using ChatterBox.Communication.Contracts;
using ChatterBox.Communication.Messages.Relay;
using Org.WebRtc;

namespace ChatterBox.Background
{
    internal sealed class Hub : IHub
    {
        private static volatile Hub _instance;
        private static readonly object SyncRoot = new object();

        private ICallChannel _callChannel;
        private CallContext _callContext;
        private AppServiceConnection _foregroundConnection;


        private MediaSettingsChannel _mediaSettingsChannel;


        private SignalingClient _signalingClient;

        private Hub()
        {
        }

        public ICallChannel CallChannel
        {
            get
            {
                SetupCallContext();
                return _callChannel;
            }
        }

        public ForegroundClient ForegroundClient { get; } = new ForegroundClient();

        public AppServiceConnection ForegroundConnection
        {
            get { return _foregroundConnection; }
            set
            {
                if (_foregroundConnection != null)
                {
                    _foregroundConnection.RequestReceived -= HandleForegroundRequest;
                }
                _foregroundConnection = value;

                if (_foregroundConnection != null)
                {
                    _foregroundConnection.RequestReceived += HandleForegroundRequest;
                }
            }
        }

        public IBackgroundTask ForegroundTask { get; set; }

        public static Hub Instance
        {
            get
            {
                if (_instance != null) return _instance;
                lock (SyncRoot)
                {
                    if (_instance == null) _instance = new Hub();
                }

                return _instance;
            }
        }

        public MediaSettingsChannel MediaSettingsChannel
        {
            get
            {
                if (_mediaSettingsChannel == null)
                {
                    _mediaSettingsChannel = new MediaSettingsChannel();
                    _mediaSettingsChannel.OnVideoDeviceSelectionChanged += OnVideoDeviceSelectionChanged;
                }
                return _mediaSettingsChannel;
            }
        }

        public StatsManager RtcStatsManager { get; } = new StatsManager();

        public SignalingClient SignalingClient
            =>
                _signalingClient ??
                (_signalingClient = new SignalingClient(SignalingSocketChannel, ForegroundClient, CallChannel));

        public SignalingSocketChannel SignalingSocketChannel { get; } = new SignalingSocketChannel();

        public VoipTask VoipTaskInstance { get; set; }

        public string WebRtcTraceFolderToken { get; set; }

        public void InitialiazeStatsManager(RTCPeerConnection pc)
        {
            RtcStatsManager.Initialize(pc);
        }

        public bool IsAppInsightsEnabled
        {
            get { return SignalingSettings.AppInsightsEnabled; }
            set
            {
                SignalingSettings.AppInsightsEnabled = value;
                RtcStatsManager.DisableTelemetry(!value);
            }
        }

        public async Task OnCallStatusAsync(CallStatus callStatus)
        {
            await ForegroundClient.OnCallStatusAsync(callStatus);
        }

        public async Task OnChangeMediaDevices(MediaDevicesChange mediaDeviceChange)
        {
            await ForegroundClient.OnChangeMediaDevicesAsync(mediaDeviceChange);
        }

        public async Task OnUpdateFrameFormat(FrameFormat frameFormat)
        {
            await ForegroundClient.OnUpdateFrameFormatAsync(frameFormat);
        }

        public async Task OnUpdateFrameRate(FrameRate frameRate)
        {
            await ForegroundClient.OnUpdateFrameRateAsync(frameRate);
        }

        public async Task Relay(RelayMessage message)
        {
            await SignalingClient.RelayAsync(message);
        }

        public void StartStatsManagerCallWatch()
        {
            RtcStatsManager.StartCallWatch();
        }

        public void StopStatsManagerCallWatch()
        {
            RtcStatsManager.StopCallWatch();
        }

        public void ToggleStatsManagerConnectionState(bool enable)
        {
            RtcStatsManager.IsStatsCollectionEnabled = enable;
        }

        public void TrackStatsManagerEvent(string name, IDictionary<string, string> props)
        {
            RtcStatsManager.TrackEvent(name, props);
        }

        public void TrackStatsManagerMetric(string name, double value)
        {
            RtcStatsManager.TrackMetric(name, value);
        }


        /// <summary>
        ///     Handles requests from the foreground by invoking the requested methods on a handler object
        /// </summary>
        private void HandleForegroundRequest(
            AppServiceConnection sender,
            AppServiceRequestReceivedEventArgs args)
        {
            var deferral = args.GetDeferral();
            try
            {
                //Identiy the channel
                var channel = args.Request.Message.Single().Key;

                //Retrieve the message (format: <Method> <Argument - can be null and is serialized as JSON>)
                var message = args.Request.Message.Single().Value.ToString();

                //Invoke the requested method on the handler based on the channel type
                if (channel == nameof(ISignalingSocketChannel))
                {
                    AppServiceChannelHelper.HandleRequest(args.Request, SignalingSocketChannel, message);
                }
                if (channel == nameof(IClientChannel))
                {
                    AppServiceChannelHelper.HandleRequest(args.Request, SignalingClient, message);
                }

                if (channel == nameof(ICallChannel))
                {
                    AppServiceChannelHelper.HandleRequest(args.Request, CallChannel, message);
                }
                if (channel == nameof(IMediaSettingsChannel))
                {
                    AppServiceChannelHelper.HandleRequest(args.Request, MediaSettingsChannel, message);
                }
            }
            finally
            {
                deferral.Complete();
            }
        }

        private void SetupCallContext()
        {
            if (_callChannel != null) return;

            var voipHelper = new VoipHelper();
            _callContext = new CallContext(this, voipHelper);
            _callChannel = new CallChannel(this, _callContext);
        }

        private async void OnVideoDeviceSelectionChanged()
        {
            if (_callContext == null)
                return;
            if (_callContext.CallType == CallType.AudioVideo)
            {
                await _callContext.WithState(st => st.CameraSelectionChanged());
            }
        }
    }
}