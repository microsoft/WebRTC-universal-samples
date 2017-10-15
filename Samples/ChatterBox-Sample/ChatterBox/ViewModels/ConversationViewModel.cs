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
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Phone.Media.Devices;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using ChatterBox.Background;
using ChatterBox.Background.AppService;
using ChatterBox.Background.AppService.Dto;
using ChatterBox.Background.Avatars;
using ChatterBox.Background.Settings;
using ChatterBox.Background.Signalling.PersistedData;
using ChatterBox.Client.WebRTCSwapChainPanel;
using ChatterBox.Communication.Contracts;
using ChatterBox.Communication.Messages.Relay;
using ChatterBox.Controls;
using ChatterBox.MVVM;
using ChatterBox.Services;
using ChatterBoxClient.Universal.BackgroundRenderer;
using CallState = ChatterBox.Controls.CallState;

namespace ChatterBox.ViewModels
{
    public class ConversationViewModel : BindableBase, IDisposable, IConversation
    {
        private readonly ICallChannel _callChannel;
        private readonly IClientChannel _clientChannel;
        private readonly IMediaSettingsChannel _settingsChannel;
        private readonly IForegroundUpdateService _foregroundUpdateService;
        private CoreDispatcher _uiDispatcher;

        private CallState _callState;
        private bool _canCloseConversation;
        private string _instantMessage;

        private bool _isAudioOnlyCall;
        private bool _isHighlighted;
        private bool _isMicEnabled = true;
        private bool _isOnline;
        private bool _isOtherConversationInCallMode;

        private bool _isPeerVideoAvailable;
        private bool _isSelected;

        private bool _isSelfVideoAvailable;
        private bool _isVideoEnabled;
        private string _localFrameRate;
        private Size _localNativeVideoSize;
        private long _localSwapChainHandle;
        private Size _localVideoControlSize;

        private UIElement _localVideoRenderer;
        private string _name;
        private ImageSource _profileSource;
        private string _remoteFrameRate;
        private Size _remoteNativeVideoSize;
        private long _remoteSwapChainHandle;
        private Size _remoteVideoControlSize;
        private UIElement _remoteVideoRenderer;

        private bool _showVideoOffButton;
        private bool _showVideoOnButton;
        private string _userId;

        private bool _speakerPhoneActive;
        private bool _showActivateSpeakerPhoneButton;
        private bool _showDeactivateSpeakerPhoneButton;

        private MediaDevice _camera;
        private MediaDevices _cameras;
        private bool _showSwitchToFrontCamera;
        private bool _showSwitchToBackCamera;
        private bool _isCameraSwitchInProgress;

        public ConversationViewModel(IClientChannel clientChannel,
            IForegroundUpdateService foregroundUpdateService,
            ICallChannel callChannel, CoreDispatcher uiDispatcher,
            IMediaSettingsChannel settingsChannel)
        {
            _clientChannel = clientChannel;
            _callChannel = callChannel;
            _settingsChannel = settingsChannel;
            _uiDispatcher = uiDispatcher;
            _foregroundUpdateService = foregroundUpdateService;
            foregroundUpdateService.OnRelayMessagesUpdated += OnRelayMessagesUpdated;
            foregroundUpdateService.OnCallStatusUpdate += OnCallStatusUpdate;
            foregroundUpdateService.OnFrameFormatUpdate += OnFrameFormatUpdate;
            foregroundUpdateService.OnFrameRateUpdate += OnFrameRateUpdate;
            SendInstantMessageCommand = new DelegateCommand(OnSendInstantMessageCommandExecute,
                OnSendInstantMessageCommandCanExecute);
            AudioCallCommand = new DelegateCommand(OnCallCommandExecute, OnCallCommandCanExecute);
            VideoCallCommand = new DelegateCommand(OnVideoCallCommandExecute, OnVideoCallCommandCanExecute);
            HangupCommand = new DelegateCommand(OnHangupCommandExecute, OnHangupCommandCanExecute);
            AnswerCommand = new DelegateCommand(OnAnswerCommandExecute, OnAnswerCommandCanExecute);
            RejectCommand = new DelegateCommand(OnRejectCommandExecute, OnRejectCommandCanExecute);
            CloseConversationCommand = new DelegateCommand(OnCloseConversationCommandExecute,
                () => _canCloseConversation);
            MuteMicrophoneCommand = new DelegateCommand(MuteMicCommandExecute, MicCommandCanExecute);
            UnMuteMicrophoneCommand = new DelegateCommand(UnMuteCommandExecute, MicCommandCanExecute);
            SwitchVideoCommand = new DelegateCommand(SwitchVideoCommandExecute, SwitchVideoCommandCanExecute);

            IsAudioRoutingApiAvailable = ApiInformation.IsApiContractPresent("Windows.Phone.PhoneContract", 1);
            if (IsAudioRoutingApiAvailable)
            {
                ActivateSpeakerPhone = new DelegateCommand(ActivateSpeakerPhoneExecute, ActivateSpeakerPhoneCanExecute);
                DeactivateSpeakerPhone = new DelegateCommand(DeactivateSpeakerPhoneExecute, DeactivateSpeakerPhoneCanExecute);
                AudioRoutingManager.GetDefault().AudioEndpointChanged += AudioEndpointChanged;
            }

            ActivateFrontCamera = new DelegateCommand(ActivateFrontCameraExecute, ActivateFrontCameraCanExecute);
            ActivateBackCamera = new DelegateCommand(ActivateBackCameraExecute, ActivateBackCameraCanExecute);

            LayoutService.Instance.LayoutChanged += LayoutChanged;
            LayoutChanged(LayoutService.Instance.LayoutType);
            SetVideoPresenters();
        }

        public bool IsAudioOnlyCall
        {
            get { return _isAudioOnlyCall; }
            set
            {
                SetProperty(ref _isAudioOnlyCall, value);
                UpdateShowVideoButtonFlags();
            }
        }

        public bool IsOtherConversationInCallMode
        {
            get { return _isOtherConversationInCallMode; }
            set
            {
                if (SetProperty(ref _isOtherConversationInCallMode, value))
                    UpdateCommandStates();
            }
        }

        public string LocalFrameRate
        {
            get { return _localFrameRate; }
            set { SetProperty(ref _localFrameRate, value); }
        }

        public Size LocalNativeVideoSize
        {
            get { return _localNativeVideoSize; }
            set { SetProperty(ref _localNativeVideoSize, value); }
        }

        public long LocalSwapChainPanelHandle
        {
            get { return _localSwapChainHandle; }
            set
            {
                IsSelfVideoAvailable = value > 0;
                if (_localSwapChainHandle == value && value != 0)
                {
                    // In case the old value equals the new value, the DependencyProperty
                    // in the WebRTCSwapChainPanel will not trigger the changed handle.
                    // In order to force an update, we set it to 0 then set the real value.
                    _localSwapChainHandle = 0;
                    OnPropertyChanged(nameof(LocalSwapChainPanelHandle));
                }
                _localSwapChainHandle = value;
                // Don't use SetProperty() because it does nothing if the value
                // doesn't change but in this case it must always update the
                // swap chain panel.
                OnPropertyChanged(nameof(LocalSwapChainPanelHandle));
            }
        }

        public Size LocalVideoControlSize
        {
            get { return _localVideoControlSize; }
            set
            {
                if (SetProperty(ref _localVideoControlSize, value))
                {
                    var size = new VideoControlSize { Size = value };
                    _callChannel.OnLocalControlSizeAsync(size).AsTask().Wait();
                }
            }
        }

        public string RemoteFrameRate
        {
            get { return _remoteFrameRate; }
            set { SetProperty(ref _remoteFrameRate, value); }
        }

        public Size RemoteNativeVideoSize
        {
            get { return _remoteNativeVideoSize; }
            set { SetProperty(ref _remoteNativeVideoSize, value); }
        }

        public long RemoteSwapChainPanelHandle
        {
            get { return _remoteSwapChainHandle; }
            set
            {
                IsPeerVideoAvailable = value > 0;
                if (_remoteSwapChainHandle == value && value != 0)
                {
                    // In case the old value equals the new value, the DependencyProperty
                    // in the WebRTCSwapChainPanel will not trigger the changed handle.
                    // In order to force an update, we set it to 0 then set the real value.
                    _remoteSwapChainHandle = 0;
                    OnPropertyChanged(nameof(RemoteSwapChainPanelHandle));
                }
                _remoteSwapChainHandle = value;
                // Don't use SetProperty() because it does nothing if the value
                // doesn't change but in this case it must always update the
                // swap chain panel.
                OnPropertyChanged(nameof(RemoteSwapChainPanelHandle));
            }
        }

        public Size RemoteVideoControlSize
        {
            get { return _remoteVideoControlSize; }
            set
            {
                if (SetProperty(ref _remoteVideoControlSize, value))
                {
                    RemoteNativeVideoSizeChanged?.Invoke();
                    var size = new VideoControlSize { Size = value };
                    _callChannel.OnRemoteControlSizeAsync(size).AsTask().Wait();
                }
            }
        }

        public bool ShowPeerVideoPlaceHolder => !(IsPeerVideoAvailable && CallState == CallState.Connected);

        public bool ShowSelfVideoPlaceHolder => !(IsSelfVideoAvailable && CallState == CallState.Connected);

        public bool ShowVideoOffButton
        {
            get { return _showVideoOffButton; }
            set { SetProperty(ref _showVideoOffButton, value); }
        }

        public bool ShowVideoOnButton
        {
            get { return _showVideoOnButton; }
            set { SetProperty(ref _showVideoOnButton, value); }
        }


        public string UserId
        {
            get { return _userId; }
            set { SetProperty(ref _userId, value); }
        }

        public bool SpeakerPhoneActive
        {
            get { return _speakerPhoneActive; }
            set
            {
                SetProperty(ref _speakerPhoneActive, value);
                UpdateSpeakerPhoneButtonFlags();
            }
        }

        public bool ShowActivateSpeakerPhoneButton
        {
            get { return _showActivateSpeakerPhoneButton; }
            set { SetProperty(ref _showActivateSpeakerPhoneButton, value); }
        }

        public bool ShowDeactivateSpeakerPhoneButton
        {
            get { return _showDeactivateSpeakerPhoneButton; }
            set { SetProperty(ref _showDeactivateSpeakerPhoneButton, value); }
        }

        public bool ShowSwitchToBackCamera
        { 
            get { return _showSwitchToBackCamera; }
            set { SetProperty(ref _showSwitchToBackCamera, value); }
        }

        public bool IsCameraSwitchInProgress
        {
            get { return _isCameraSwitchInProgress; }
            set
            {
                SetProperty(ref _isCameraSwitchInProgress, value);
                UpdateCommandStates();
            }
        }
        

        public bool ShowSwitchToFrontCamera
        {
            get { return _showSwitchToFrontCamera; }
            set { SetProperty(ref _showSwitchToFrontCamera, value); }
        }

        public ICommand AnswerCommand { get; }
        public ICommand AudioCallCommand { get; }

        public CallState CallState
        {
            get { return _callState; }
            set
            {
                if (!SetProperty(ref _callState, value)) return;
                if (value != CallState.Idle) OnIsInCallMode?.Invoke(this);
                UpdateCommandStates();
                UpdateSpeakerPhoneButtonFlags();
                UpdateCameraSwitchButtonFlags();

                OnPropertyChanged(nameof(ShowPeerVideoPlaceHolder));
                OnPropertyChanged(nameof(ShowSelfVideoPlaceHolder));
            }
        }


        public ICommand CloseConversationCommand { get; }
        public ICommand HangupCommand { get; }

        public string InstantMessage
        {
            get { return _instantMessage; }
            set
            {
                if (SetProperty(ref _instantMessage, value))
                {
                    ((DelegateCommand) SendInstantMessageCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public IEnumerable<IInstantMessage> InstantMessages { get; } =
            new ObservableCollection<InstantMessageViewModel>();

        public bool IsHighlighted
        {
            get { return _isHighlighted; }
            set { SetProperty(ref _isHighlighted, value); }
        }

        public bool IsMicrophoneEnabled
        {
            get { return _isMicEnabled; }
            set { SetProperty(ref _isMicEnabled, value); }
        }

        public bool IsOnline
        {
            get { return _isOnline; }
            set { SetProperty(ref _isOnline, value); }
        }

        public bool IsPeerVideoAvailable
        {
            get { return _isPeerVideoAvailable; }
            set
            {
                if (SetProperty(ref _isPeerVideoAvailable, value))
                {
                    OnPropertyChanged(nameof(ShowPeerVideoPlaceHolder));
                }
            }
        }

        public bool IsSelfVideoAvailable
        {
            get { return _isSelfVideoAvailable; }
            set
            {
                if (SetProperty(ref _isSelfVideoAvailable, value))
                {
                    OnPropertyChanged(nameof(ShowSelfVideoPlaceHolder));
                }
            }
        }

        public bool IsVideoEnabled
        {
            get { return _isVideoEnabled; }
            set
            {
                SetProperty(ref _isVideoEnabled, value);
                UpdateShowVideoButtonFlags();
            }
        }

        public UIElement LocalVideoRenderer
        {
            get { return _localVideoRenderer; }
            set { SetProperty(ref _localVideoRenderer, value); }
        }

        public string Name
        {
            get { return _name; }
            set { SetProperty(ref _name, value); }
        }

        public ImageSource OwnProfileSource { get; } =
            new BitmapImage(new Uri(AvatarLink.EmbeddedLinkFor(SignallingStatus.Avatar)));

        public ImageSource ProfileSource
        {
            get { return _profileSource; }
            set { SetProperty(ref _profileSource, value); }
        }

        public ICommand RejectCommand { get; }

        public UIElement RemoteVideoRenderer
        {
            get { return _remoteVideoRenderer; }
            set { SetProperty(ref _remoteVideoRenderer, value); }
        }

        public bool IsAudioRoutingApiAvailable { get; }

        public ICommand SendInstantMessageCommand { get; }
        public ICommand SwitchVideoCommand { get; }
        public ICommand MuteMicrophoneCommand { get; }
        public ICommand UnMuteMicrophoneCommand { get; }
        public ICommand VideoCallCommand { get; }
        public ICommand ActivateSpeakerPhone { get; }
        public ICommand DeactivateSpeakerPhone { get; }
        public ICommand ActivateFrontCamera { get; }
        public ICommand ActivateBackCamera { get; }

        // Avoid memory leak by unsubscribing from foregroundUpdateService object
        // because its lifetime may be much longer.
        public void Dispose()
        {
            if (_foregroundUpdateService == null) return;

            _foregroundUpdateService.OnRelayMessagesUpdated -= OnRelayMessagesUpdated;
            _foregroundUpdateService.OnCallStatusUpdate -= OnCallStatusUpdate;
            _foregroundUpdateService.OnFrameFormatUpdate -= OnFrameFormatUpdate;
            if(IsAudioRoutingApiAvailable)
            {
                AudioRoutingManager.GetDefault().AudioEndpointChanged -= AudioEndpointChanged;
            }
        }

        public async Task InitializeAsync()
        {
            var callStatus = await _callChannel.GetCallStatusAsync();
            if (callStatus != null)
            {
                await CallStatusUpdate(callStatus);
            }

            if (CallState != CallState.Idle)
            {
                FrameFormat localFrameFormat = await _callChannel.GetFrameFormatAsync(true);
                if (localFrameFormat != null)
                {
                    OnFrameFormatUpdate(localFrameFormat);
                }

                FrameFormat remoteFrameFormat = await _callChannel.GetFrameFormatAsync(false);
                if (remoteFrameFormat != null)
                {
                    OnFrameFormatUpdate(remoteFrameFormat);
                }
            }

            // Get stored relay messages
            OnRelayMessagesUpdated();

            _cameras = await _settingsChannel.GetVideoCaptureDevicesAsync();
        }

        public event Action<ConversationViewModel> OnCloseConversation;

        public event Action<ConversationViewModel> OnIsInCallMode;

        public event Action RemoteNativeVideoSizeChanged;

        public override string ToString()
        {
            return $"{Name}";
        }

        internal void OnNavigatedFrom()
        {
            _isSelected = false;
            foreach (var msg in InstantMessages)
            {
                msg.IsHighlighted = false;
            }
        }

        internal void OnNavigatedTo()
        {
            _isSelected = true;
            IsHighlighted = false;
        }

        private void LayoutChanged(LayoutType state)
        {
            _canCloseConversation = state == LayoutType.Overlay;
            ((DelegateCommand) CloseConversationCommand).RaiseCanExecuteChanged();
        }

        private bool MicCommandCanExecute()
        {
            return CallState != CallState.Idle && CallState != CallState.Held;
        }

        private async void MuteMicCommandExecute()
        {
            IsMicrophoneEnabled = false;
            await _callChannel.ConfigureMicrophoneAsync(new MicrophoneConfig
            {
                Muted = !IsMicrophoneEnabled
            });
        }

        private bool OnAnswerCommandCanExecute()
        {
            return CallState == CallState.LocalRinging;
        }

        private async void OnAnswerCommandExecute()
        {
            await _callChannel.AnswerAsync();
        }

        private bool OnCallCommandCanExecute()
        {
            return (CallState == CallState.Idle) && !IsOtherConversationInCallMode;
        }

        private async void OnCallCommandExecute()
        {
            IsAudioOnlyCall = true;
            await _callChannel.CallAsync(new OutgoingCallRequest
            {
                PeerUserId = UserId,
                VideoEnabled = false
            });
            IsSelfVideoAvailable = false;
        }

        private async void OnCallStatusUpdate(CallStatus callState)
        {
            await CallStatusUpdate(callState);
        }

        private async Task CallStatusUpdate(CallStatus callState)
        {
            switch (callState.State)
            {
                case Background.AppService.Dto.CallState.Idle:
                    CallState = CallState.Idle;
                    IsOtherConversationInCallMode = false;
                    LocalSwapChainPanelHandle = 0;
                    RemoteSwapChainPanelHandle = 0;
                    _camera = null;
                    break;
                case Background.AppService.Dto.CallState.LocalRinging:
                    if (callState.PeerId == UserId)
                    {
                        CallState = CallState.LocalRinging;
                        UnMuteCommandExecute(); //Start new calls with mic enabled
                        IsVideoEnabled = callState.IsVideoEnabled;
                        IsAudioOnlyCall = callState.Type == CallType.Audio;
                        LocalNativeVideoSize = new Size(0, 0);
                        RemoteNativeVideoSize = new Size(0, 0);
                        LocalFrameRate = "N/A";
                        RemoteFrameRate = "N/A";
                    }
                    else
                    {
                        IsOtherConversationInCallMode = true;
                    }
                    break;
                case Background.AppService.Dto.CallState.RemoteRinging:
                    if (callState.PeerId == UserId)
                    {
                        CallState = CallState.RemoteRinging;
                        UnMuteCommandExecute(); //Start new calls with mic enabled
                        IsVideoEnabled = callState.IsVideoEnabled;
                        IsAudioOnlyCall = callState.Type == CallType.Audio;
                        LocalNativeVideoSize = new Size(0, 0);
                        RemoteNativeVideoSize = new Size(0, 0);
                        LocalFrameRate = "N/A";
                        RemoteFrameRate = "N/A";
                    }
                    else
                    {
                        IsOtherConversationInCallMode = true;
                    }
                    break;
                case Background.AppService.Dto.CallState.EstablishOutgoing:
                    if (callState.PeerId == UserId)
                    {
                        CallState = CallState.Connected;
                        IsVideoEnabled = callState.IsVideoEnabled;
                        IsAudioOnlyCall = callState.Type == CallType.Audio;
                        IsSelfVideoAvailable = IsVideoEnabled;
                        IsPeerVideoAvailable = callState.Type == CallType.AudioVideo;
                    }
                    else
                    {
                        IsOtherConversationInCallMode = true;
                    }
                    break;
                case Background.AppService.Dto.CallState.EstablishIncoming:
                    if (callState.PeerId == UserId)
                    {
                        CallState = CallState.Connected;
                        IsVideoEnabled = callState.IsVideoEnabled;
                        IsAudioOnlyCall = callState.Type == CallType.Audio;
                        IsSelfVideoAvailable = IsVideoEnabled;
                        IsPeerVideoAvailable = callState.Type == CallType.AudioVideo;
                    }
                    else
                    {
                        IsOtherConversationInCallMode = true;
                    }
                    break;
                case Background.AppService.Dto.CallState.HangingUp:
                    if (callState.PeerId == UserId)
                    {
                        CallState = CallState.Connected;
                    }
                    else
                    {
                        IsOtherConversationInCallMode = true;
                    }
                    break;
                case Background.AppService.Dto.CallState.Held:
                    if (callState.PeerId == UserId)
                    {
                        CallState = CallState.Held;
                    }
                    break;
                case Background.AppService.Dto.CallState.ActiveCall:
                    IsVideoEnabled = callState.IsVideoEnabled;
                    IsAudioOnlyCall = callState.Type == CallType.Audio;
                    if (callState.PeerId == UserId)
                    {
                        CallState = CallState.Connected;
                        IsSelfVideoAvailable = IsVideoEnabled;
                        IsPeerVideoAvailable = callState.Type == CallType.AudioVideo;

                        _camera = await _settingsChannel.GetVideoDeviceAsync();
                        IsCameraSwitchInProgress = false;
                        UpdateCameraSwitchButtonFlags();
                    }
                    else
                    {
                        IsOtherConversationInCallMode = true;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnCloseConversationCommandExecute()
        {
            OnCloseConversation?.Invoke(this);
        }

        private void OnFrameFormatUpdate(FrameFormat obj)
        {
            if (CallState == CallState.Idle)
            {
                return;
            }

            if (obj.ForegroundProcessId != Renderer.GetProcessId())
            {
                // Ignore this update because it's for an old foreground process
                return;
            }

            if (obj.IsLocal)
            {
                LocalSwapChainPanelHandle = obj.SwapChainHandle;
                LocalNativeVideoSize = new Size(obj.Width, obj.Height);
            }
            else
            {
                RemoteSwapChainPanelHandle = obj.SwapChainHandle;
                RemoteNativeVideoSize = new Size(obj.Width, obj.Height);
            }
        }

        private void OnFrameRateUpdate(FrameRate obj)
        {
            if (obj.IsLocal)
                LocalFrameRate = obj.Fps;
            else
                RemoteFrameRate = obj.Fps;
        }

        private bool OnHangupCommandCanExecute()
        {
            return (CallState == CallState.Connected)
                || (CallState == CallState.RemoteRinging)
                || (CallState == CallState.Held);
        }

        private async void OnHangupCommandExecute()
        {
            await _callChannel.HangupAsync();
        }

        private bool OnRejectCommandCanExecute()
        {
            return CallState == CallState.LocalRinging;
        }

        private async void OnRejectCommandExecute()
        {
            await _callChannel.RejectAsync(new IncomingCallReject
            {
                Reason = "Rejected"
            });
        }

        private async void OnRelayMessagesUpdated()
        {
            var newMessages = await SignaledInstantMessages.GetAllFromAsync(_userId);
            var filteredNewMessages = newMessages
                .OrderBy(s => s.SentDateTimeUtc).ToList();

            foreach (var message in filteredNewMessages)
            {
                await SignaledInstantMessages.DeleteAsync(message.Id);
                ((ObservableCollection<InstantMessageViewModel>) InstantMessages).Add(new InstantMessageViewModel
                {
                    Body = message.Payload,
                    DeliveredAt = message.SentDateTimeUtc.LocalDateTime,
                    IsSender = false,
                    SenderName = Name,
                    SenderProfileSource = ProfileSource,
                    IsHighlighted = !_isSelected
                });
                ETWEventLogger.Instance.LogEvent("Incomming Message",
                    "SenderName is " + message.FromName + " ReceiverName is " + RegistrationSettings.Name
                    + "\n SendDateTimeUtc " + message.SentDateTimeUtc.ToString(),
                    DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString());
            }
            if (!_isSelected && filteredNewMessages.Count > 0)
            {
                IsHighlighted = true;
            }
        }

        private bool OnSendInstantMessageCommandCanExecute()
        {
            return !string.IsNullOrWhiteSpace(InstantMessage);
        }

        private async void OnSendInstantMessageCommandExecute()
        {
            var message = new RelayMessage
            {
                SentDateTimeUtc = DateTimeOffset.UtcNow,
                ToUserId = UserId,
                FromUserId = RegistrationSettings.UserId,
                Payload = InstantMessage.Trim(),
                Tag = RelayMessageTags.InstantMessage
            };
            InstantMessage = null;
            await _clientChannel.RelayAsync(message);
            ((ObservableCollection<InstantMessageViewModel>) InstantMessages).Add(new InstantMessageViewModel
            {
                Body = message.Payload,
                DeliveredAt = message.SentDateTimeUtc.LocalDateTime,
                IsSender = true,
                SenderName = RegistrationSettings.Name,
                SenderProfileSource = OwnProfileSource
            });
            ETWEventLogger.Instance.LogEvent("Outgoing Message",
                "SenderName is " + RegistrationSettings.Name + " ReceiverName is " + Name
                + "\n SendDateTimeUtc " + message.SentDateTimeUtc.ToString(),
                DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString());
        }

        private bool OnVideoCallCommandCanExecute()
        {
            return (CallState == CallState.Idle) && !IsOtherConversationInCallMode;
        }

        private async void OnVideoCallCommandExecute()
        {
            IsAudioOnlyCall = false;
            await _callChannel.CallAsync(new OutgoingCallRequest
            {
                PeerUserId = UserId,
                VideoEnabled = true
            });
        }

        private void SetVideoPresenters()
        {
            var remoteVideoRenderer = new WebRTCSwapChainPanel();
            remoteVideoRenderer.SizeChanged += (s, e) => { RemoteVideoControlSize = e.NewSize; };

            remoteVideoRenderer.SetBinding(
                WebRTCSwapChainPanel.SwapChainPanelHandleProperty,
                new Binding
                {
                    Source = this,
                    Path = new PropertyPath(nameof(RemoteSwapChainPanelHandle)),
                    Mode = BindingMode.OneWay
                });
            RemoteVideoRenderer = remoteVideoRenderer;

            var localVideoRenderer = new WebRTCSwapChainPanel();
            localVideoRenderer.SizeChanged += (s, e) => { LocalVideoControlSize = e.NewSize; };

            localVideoRenderer.SetBinding(
                WebRTCSwapChainPanel.SwapChainPanelHandleProperty,
                new Binding
                {
                    Source = this,
                    Path = new PropertyPath(nameof(LocalSwapChainPanelHandle)),
                    Mode = BindingMode.OneWay
                });
            LocalVideoRenderer = localVideoRenderer;
        }

        private bool SwitchVideoCommandCanExecute()
        {
            return CallState != CallState.Idle && CallState != CallState.Held;
        }

        private async void SwitchVideoCommandExecute()
        {
            IsVideoEnabled = !IsVideoEnabled;
            await _callChannel.ConfigureVideoAsync(new VideoConfig
            {
                On = IsVideoEnabled
            });
            IsSelfVideoAvailable = IsVideoEnabled;
        }

        private async void UnMuteCommandExecute()
        {
            IsMicrophoneEnabled = true;
            await _callChannel.ConfigureMicrophoneAsync(new MicrophoneConfig
            {
                Muted = !IsMicrophoneEnabled
            });
        }

        private bool ActivateSpeakerPhoneCanExecute()
        {
            return IsAudioRoutingApiAvailable && CallState == CallState.Connected;
        }

        private void ActivateSpeakerPhoneExecute()
        {
            var routingManager = AudioRoutingManager.GetDefault();
            routingManager.SetAudioEndpoint(AudioRoutingEndpoint.Speakerphone);
        }

        private bool DeactivateSpeakerPhoneCanExecute()
        {
            return IsAudioRoutingApiAvailable && CallState == CallState.Connected;
        }

        private void DeactivateSpeakerPhoneExecute()
        {
            var routingManager = AudioRoutingManager.GetDefault();
            if (routingManager.AvailableAudioEndpoints.HasFlag(AvailableAudioRoutingEndpoints.Bluetooth))
            {
                routingManager.SetAudioEndpoint(AudioRoutingEndpoint.Bluetooth);
            }
            else if (routingManager.AvailableAudioEndpoints.HasFlag(AvailableAudioRoutingEndpoints.Earpiece))
            {
                routingManager.SetAudioEndpoint(AudioRoutingEndpoint.Earpiece);
            }
        }

        private async void AudioEndpointChanged(AudioRoutingManager sender, object args)
        {
            await RunOnUiThread(() =>
            {
                var routingManager = AudioRoutingManager.GetDefault();
                SpeakerPhoneActive = (routingManager.GetAudioEndpoint() == AudioRoutingEndpoint.Speakerphone);
            });
        }

        private void UpdateSpeakerPhoneButtonFlags()
        {
            ShowActivateSpeakerPhoneButton = CallState == CallState.Connected &&
                IsAudioRoutingApiAvailable && !SpeakerPhoneActive;
            ShowDeactivateSpeakerPhoneButton = CallState == CallState.Connected &&
                IsAudioRoutingApiAvailable && SpeakerPhoneActive;
        }

        private async void ActivateFrontCameraExecute()
        {
            var frontCamera = _cameras.Devices.FirstOrDefault(c => c.Location == MediaDeviceLocation.Front);
            if(frontCamera != null)
            {
                IsCameraSwitchInProgress = true;
                await _settingsChannel.SetVideoDeviceAsync(frontCamera);
            }
        }

        private bool ActivateFrontCameraCanExecute()
        {
            return CallState == CallState.Connected && !IsCameraSwitchInProgress;
        }

        private async void ActivateBackCameraExecute()
        {
            var backCamera = _cameras.Devices.FirstOrDefault(c => c.Location == MediaDeviceLocation.Back);
            if (backCamera != null)
            {
                IsCameraSwitchInProgress = true;
                await _settingsChannel.SetVideoDeviceAsync(backCamera);
            }
        }

        private bool ActivateBackCameraCanExecute()
        {
            return CallState == CallState.Connected && !IsCameraSwitchInProgress;
        }

        private void UpdateCameraSwitchButtonFlags()
        {
            ShowSwitchToBackCamera =
                CallState == CallState.Connected &&
                !IsAudioOnlyCall &&
                _camera != null &&
                _camera.Location == MediaDeviceLocation.Front &&
                _cameras != null &&
                _cameras.Devices.FirstOrDefault(c => c.Location == MediaDeviceLocation.Back) != null;
            ShowSwitchToFrontCamera =
                CallState == CallState.Connected &&
                !IsAudioOnlyCall &&
                _camera != null &&
                _camera.Location == MediaDeviceLocation.Back &&
                _cameras != null &&
                _cameras.Devices.FirstOrDefault(c => c.Location == MediaDeviceLocation.Front) != null;
        }

        protected IAsyncAction RunOnUiThread(Action fn)
        {
            return _uiDispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(fn));
        }

        private void UpdateCommandStates()
        {
            ((DelegateCommand) AudioCallCommand).RaiseCanExecuteChanged();
            ((DelegateCommand) VideoCallCommand).RaiseCanExecuteChanged();
            ((DelegateCommand) AnswerCommand).RaiseCanExecuteChanged();
            ((DelegateCommand) HangupCommand).RaiseCanExecuteChanged();
            ((DelegateCommand) RejectCommand).RaiseCanExecuteChanged();
            ((DelegateCommand) MuteMicrophoneCommand).RaiseCanExecuteChanged();
            ((DelegateCommand) UnMuteMicrophoneCommand).RaiseCanExecuteChanged();
            ((DelegateCommand) SwitchVideoCommand).RaiseCanExecuteChanged();
            if(ActivateSpeakerPhone != null)
                ((DelegateCommand) ActivateSpeakerPhone).RaiseCanExecuteChanged();
            if (DeactivateSpeakerPhone != null)
                ((DelegateCommand) DeactivateSpeakerPhone).RaiseCanExecuteChanged();
            if (ActivateFrontCamera != null)
                ((DelegateCommand) ActivateFrontCamera).RaiseCanExecuteChanged();
            if (ActivateBackCamera != null)
                ((DelegateCommand) ActivateBackCamera).RaiseCanExecuteChanged();
        }

        private void UpdateShowVideoButtonFlags()
        {
            ShowVideoOnButton = !IsAudioOnlyCall && IsVideoEnabled;
            ShowVideoOffButton = !IsAudioOnlyCall && !IsVideoEnabled;
        }
    }
}