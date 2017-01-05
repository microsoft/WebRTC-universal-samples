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
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using ChatterBox.Background.AppService;
using ChatterBox.Background.AppService.Dto;
using ChatterBox.Background.Helpers;
using ChatterBox.Background.Tasks;
using ChatterBox.Communication.Contracts;
using ChatterBox.Communication.Messages.Registration;
using ChatterBox.Communication.Messages.Relay;
using ChatterBox.Communication.Messages.Standard;
using ChatterBox.MVVM;

namespace ChatterBox.Services
{
    public class HubClient : DispatcherBindableBase,
        IForegroundUpdateService,
        ISignalingSocketChannel,
        IClientChannel,
        ICallChannel,
        IForegroundChannel,
        IMediaSettingsChannel
    {
        private readonly TaskHelper _taskHelper;
        private AppServiceConnection _appConnection;

        public HubClient(CoreDispatcher uiDispatcher, TaskHelper taskHelper) : base(uiDispatcher)
        {
            _taskHelper = taskHelper;
        }

        public bool IsConnected { get; private set; }

        public IAsyncAction AnswerAsync()
        {
            return InvokeHubChannelAsync<ICallChannel>().AsTask().AsAsyncAction();
        }

        public IAsyncAction CallAsync(OutgoingCallRequest request)
        {
            return InvokeHubChannelAsync<ICallChannel>(request).AsTask().AsAsyncAction();
        }

        public IAsyncAction ConfigureMicrophoneAsync(MicrophoneConfig config)
        {
            return InvokeHubChannelAsync<ICallChannel>(config).AsTask().AsAsyncAction();
        }

        public IAsyncAction ConfigureVideoAsync(VideoConfig config)
        {
            return InvokeHubChannelAsync<ICallChannel>(config).AsTask().AsAsyncAction();
        }

        public IAsyncAction DisplayOrientationChangedAsync(DisplayOrientations orientation)
        {
            return InvokeHubChannelAsync<ICallChannel>(orientation).AsTask().AsAsyncAction();
        }

        public IAsyncOperation<CallStatus> GetCallStatusAsync()
        {
            return InvokeHubChannelAsync<ICallChannel, CallStatus>();
        }

        public IAsyncOperation<FrameFormat> GetFrameFormatAsync(bool local)
        {
            return InvokeHubChannelAsync<ICallChannel, FrameFormat>(local);
        }

        public IAsyncAction HangupAsync()
        {
            return InvokeHubChannelAsync<ICallChannel>().AsTask().AsAsyncAction();
        }

        public IAsyncAction InitializeRtcAsync()
        {
            return InvokeHubChannelAsync<ICallChannel>().AsTask().AsAsyncAction();
        }

        public IAsyncAction OnIceCandidateAsync(RelayMessage message)
        {
            return InvokeHubChannelAsync<ICallChannel>(message).AsTask().AsAsyncAction();
        }

        public IAsyncAction OnIncomingCallAsync(RelayMessage message)
        {
            return InvokeHubChannelAsync<ICallChannel>(message).AsTask().AsAsyncAction();
        }

        public IAsyncAction OnLocalControlSizeAsync(VideoControlSize size)
        {
            return InvokeHubChannelAsync<ICallChannel>(size).AsTask().AsAsyncAction();
        }

        public IAsyncAction OnOutgoingCallAcceptedAsync(RelayMessage message)
        {
            return InvokeHubChannelAsync<ICallChannel>(message).AsTask().AsAsyncAction();
        }

        public IAsyncAction OnOutgoingCallRejectedAsync(RelayMessage message)
        {
            return InvokeHubChannelAsync<ICallChannel>(message).AsTask().AsAsyncAction();
        }

        public IAsyncAction OnRemoteControlSizeAsync(VideoControlSize size)
        {
            return InvokeHubChannelAsync<ICallChannel>(size).AsTask().AsAsyncAction();
        }

        public IAsyncAction OnRemoteHangupAsync(RelayMessage message)
        {
            return InvokeHubChannelAsync<ICallChannel>(message).AsTask().AsAsyncAction();
        }

        public IAsyncAction OnSdpAnswerAsync(RelayMessage message)
        {
            return InvokeHubChannelAsync<ICallChannel>(message).AsTask().AsAsyncAction();
        }

        public IAsyncAction OnSdpOfferAsync(RelayMessage message)
        {
            return InvokeHubChannelAsync<ICallChannel>(message).AsTask().AsAsyncAction();
        }

        public IAsyncAction RejectAsync(IncomingCallReject reason)
        {
            return InvokeHubChannelAsync<ICallChannel>(reason).AsTask().AsAsyncAction();
        }

        public IAsyncAction ResumeCallVideoAsync()
        {
            return InvokeHubChannelAsync<ICallChannel>().AsTask().AsAsyncAction();
        }


        public IAsyncAction SetForegroundProcessIdAsync(uint processId)
        {
            return InvokeHubChannelAsync<ICallChannel>(processId).AsTask().AsAsyncAction();
        }

        public IAsyncAction SuspendCallVideoAsync()
        {
            return InvokeHubChannelAsync<ICallChannel>().AsTask().AsAsyncAction();
        }

        public IAsyncAction HoldAsync()
        {
            return InvokeHubChannelAsync<ICallChannel>().AsTask().AsAsyncAction();
        }

        public IAsyncAction ResumeAsync()
        {
            return InvokeHubChannelAsync<ICallChannel>().AsTask().AsAsyncAction();
        }


        public IAsyncAction ClientConfirmationAsync(Confirmation confirmation)
        {
            return InvokeHubChannelAsync<IClientChannel>(confirmation).AsTask().AsAsyncAction();
        }

        public IAsyncAction ClientHeartBeatAsync()
        {
            return InvokeHubChannelAsync<IClientChannel>().AsTask().AsAsyncAction();
        }

        public IAsyncAction GetPeerListAsync(Message message)
        {
            return InvokeHubChannelAsync<IClientChannel>(message).AsTask().AsAsyncAction();
        }

        public IAsyncAction RegisterAsync(Registration message)
        {
            return InvokeHubChannelAsync<IClientChannel>(message).AsTask().AsAsyncAction();
        }

        public IAsyncAction RelayAsync(RelayMessage message)
        {
            return InvokeHubChannelAsync<IClientChannel>(message).AsTask().AsAsyncAction();
        }

        public IAsyncOperation<ForegroundState> GetForegroundStateAsync()
        {
            return Task.FromResult(new ForegroundState {IsForegroundVisible = true}).AsAsyncOperation();
        }

        public IAsyncOperation<string> GetShownUserIdAsync()
        {
            var showUserId = string.Empty;
            if (GetShownUser != null)
            {
                showUserId = GetShownUser();
            }
            return Task.FromResult(showUserId).AsAsyncOperation();
        }

        public IAsyncAction OnCallStatusAsync(CallStatus status)
        {
            return RunOnUiThread(() => OnCallStatusUpdate?.Invoke(status));
        }

        public IAsyncAction OnChangeMediaDevicesAsync(MediaDevicesChange mediaDevicesChange)
        {
            return RunOnUiThread(() => OnMediaDevicesChanged?.Invoke(mediaDevicesChange));
        }


        public IAsyncAction OnSignaledPeerDataUpdatedAsync()
        {
            return RunOnUiThread(() => OnPeerDataUpdated?.Invoke());
        }

        public IAsyncAction OnSignaledRegistrationStatusUpdatedAsync()
        {
            return RunOnUiThread(() => OnRegistrationStatusUpdated?.Invoke());
        }

        public IAsyncAction OnSignaledRelayMessagesUpdatedAsync()
        {
            return RunOnUiThread(() => OnRelayMessagesUpdated?.Invoke());
        }

        public IAsyncAction OnUpdateFrameFormatAsync(FrameFormat frameFormat)
        {
            return RunOnUiThread(() => OnFrameFormatUpdate?.Invoke(frameFormat));
        }

        public IAsyncAction OnUpdateFrameRateAsync(FrameRate frameRate)
        {
            return RunOnUiThread(() => OnFrameRateUpdate?.Invoke(frameRate));
        }

        public event Func<string> GetShownUser;
        public event Action<CallStatus> OnCallStatusUpdate;
        public event Action<FrameFormat> OnFrameFormatUpdate;
        public event Action<FrameRate> OnFrameRateUpdate;
        public event Action<MediaDevicesChange> OnMediaDevicesChanged;


        public event Action OnPeerDataUpdated;
        public event Action OnRegistrationStatusUpdated;
        public event Action OnRelayMessagesUpdated;

        public IAsyncOperation<MediaDevices> GetAudioCaptureDevicesAsync()
        {
            return InvokeHubChannelAsync<IMediaSettingsChannel, MediaDevices>();
        }

        public IAsyncOperation<CodecInfo> GetAudioCodecAsync()
        {
            return InvokeHubChannelAsync<IMediaSettingsChannel, CodecInfo>();
        }

        public IAsyncOperation<CodecInfos> GetAudioCodecsAsync()
        {
            return InvokeHubChannelAsync<IMediaSettingsChannel, CodecInfos>();
        }

        public IAsyncOperation<MediaDevice> GetAudioDeviceAsync()
        {
            return InvokeHubChannelAsync<IMediaSettingsChannel, MediaDevice>();
        }

        public IAsyncOperation<MediaDevice> GetAudioPlayoutDeviceAsync()
        {
            return InvokeHubChannelAsync<IMediaSettingsChannel, MediaDevice>();
        }

        public IAsyncOperation<MediaDevices> GetAudioPlayoutDevicesAsync()
        {
            return InvokeHubChannelAsync<IMediaSettingsChannel, MediaDevices>();
        }

        public IAsyncOperation<MediaDevices> GetVideoCaptureDevicesAsync()
        {
            return InvokeHubChannelAsync<IMediaSettingsChannel, MediaDevices>();
        }

        public IAsyncOperation<CodecInfo> GetVideoCodecAsync()
        {
            return InvokeHubChannelAsync<IMediaSettingsChannel, CodecInfo>();
        }

        public IAsyncOperation<CodecInfos> GetVideoCodecsAsync()
        {
            return InvokeHubChannelAsync<IMediaSettingsChannel, CodecInfos>();
        }

        public IAsyncOperation<MediaDevice> GetVideoDeviceAsync()
        {
            return InvokeHubChannelAsync<IMediaSettingsChannel, MediaDevice>();
        }

        public IAsyncAction ReleaseDevicesAsync()
        {
            return InvokeHubChannelAsync<IMediaSettingsChannel>().AsTask().AsAsyncAction();
        }

        public IAsyncAction SaveTraceAsync(TraceServerConfig traceServer)
        {
            return InvokeHubChannelAsync<IMediaSettingsChannel>(traceServer).AsTask().AsAsyncAction();
        }

        public IAsyncAction SetAudioCodecAsync(CodecInfo codec)
        {
            return InvokeHubChannelAsync<IMediaSettingsChannel>(codec).AsTask().AsAsyncAction();
        }

        public IAsyncAction SetAudioDeviceAsync(MediaDevice device)
        {
            return InvokeHubChannelAsync<IMediaSettingsChannel>(device).AsTask().AsAsyncAction();
        }

        public IAsyncAction SetAudioPlayoutDeviceAsync(MediaDevice device)
        {
            return InvokeHubChannelAsync<IMediaSettingsChannel>(device).AsTask().AsAsyncAction();
        }

        public IAsyncAction SetPreferredVideoCaptureFormatAsync(VideoCaptureFormat format)
        {
            return InvokeHubChannelAsync<IMediaSettingsChannel>(format).AsTask().AsAsyncAction();
        }

        public IAsyncAction SetTraceFolderTokenAsync(string token)
        {
            return InvokeHubChannelAsync<IMediaSettingsChannel>(token).AsTask().AsAsyncAction();
        }

        public IAsyncAction SetVideoCodecAsync(CodecInfo codec)
        {
            return InvokeHubChannelAsync<IMediaSettingsChannel>(codec).AsTask().AsAsyncAction();
        }

        public IAsyncAction SetVideoDeviceAsync(MediaDevice device)
        {
            return InvokeHubChannelAsync<IMediaSettingsChannel>(device).AsTask().AsAsyncAction();
        }

        public IAsyncAction StartTraceAsync()
        {
            return InvokeHubChannelAsync<IMediaSettingsChannel>().AsTask().AsAsyncAction();
        }

        public IAsyncAction StopTraceAsync()
        {
            return InvokeHubChannelAsync<IMediaSettingsChannel>().AsTask().AsAsyncAction();
        }

        public IAsyncAction SyncWithNtpAsync(long ntpTime)
        {
            return InvokeHubChannelAsync<IMediaSettingsChannel>(ntpTime).AsTask().AsAsyncAction();
        }

        public IAsyncAction ToggleEtwStatsAsync(bool enabled)
        {
            return InvokeHubChannelAsync<IMediaSettingsChannel>(enabled).AsTask().AsAsyncAction();
        }

        public IAsyncAction SetStatsConfigAsync(StatsConfig config)
        {
            return InvokeHubChannelAsync<IMediaSettingsChannel>(config).AsTask().AsAsyncAction();
        }


        public IAsyncOperation<ConnectionStatus> ConnectToSignalingServerAsync(ConnectionOwner connectionOwner)
        {
            return InvokeHubChannelAsync<ISignalingSocketChannel, ConnectionStatus>(new ConnectionOwner
            {
                OwnerId = _taskHelper.GetTask(nameof(SignalingTask)).TaskId.ToString()
            });
        }

        public IAsyncAction DisconnectSignalingServerAsync()
        {
            return InvokeHubChannelAsync<ISignalingSocketChannel>().AsTask().AsAsyncAction();
        }

        public IAsyncOperation<ConnectionStatus> GetConnectionStatusAsync()
        {
            return InvokeHubChannelAsync<ISignalingSocketChannel, ConnectionStatus>();
        }


        public async Task<bool> Connect()
        {
            _appConnection = new AppServiceConnection
            {
                AppServiceName = nameof(ForegroundAppServiceTask),
                PackageFamilyName = Package.Current.Id.FamilyName
            };
            _appConnection.ServiceClosed += OnServiceClosed;
            _appConnection.RequestReceived += OnRequestReceived;
            var status = await _appConnection.OpenAsync();
            IsConnected = status == AppServiceConnectionStatus.Success;
            return IsConnected;
        }


        public event Action OnDisconnectedFromHub;


        public void RegisterVideoElements(MediaElement self, MediaElement peer)
        {
        }

        public IAsyncOperation<bool> RequestAccessForMediaCaptureAsync()
        {
            // do not call for Windows 10
            throw new NotSupportedException();
        }

        private IAsyncOperation<AppServiceResponse> InvokeHubChannelAsync<TContract>(object arg = null,
            [CallerMemberName] string method = null)
        {
            return _appConnection.InvokeChannelAsync(typeof (TContract), arg, method);
        }

        private IAsyncOperation<TResult> InvokeHubChannelAsync<TContract, TResult>(object arg = null,
            [CallerMemberName] string method = null)
            where TResult : class
        {
            return Task.Run(async () =>
                (TResult) await _appConnection
                    .InvokeChannelAsync(typeof (TContract), arg, method, typeof (TResult))).AsAsyncOperation();
        }

        private void OnRequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            var deferral = args.GetDeferral();
            try
            {
                var message = args.Request.Message.Single().Value.ToString();
                AppServiceChannelHelper.HandleRequest(args.Request, this, message);
            }
            finally
            {
                deferral.Complete();
            }
        }

        private void OnServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            IsConnected = false;
            Debug.WriteLine("HubClient.OnServiceClosed()");
            OnDisconnectedFromHub?.Invoke();
        }
    }
}