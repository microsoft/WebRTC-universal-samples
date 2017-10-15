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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Foundation.Metadata;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.UI.Core;
using Windows.UI.Popups;
using ChatterBox.Background;
using ChatterBox.Background.AppService;
using ChatterBox.Background.AppService.Dto;
using ChatterBox.Background.Settings;
using ChatterBox.MVVM;
using ChatterBox.Services;
using Microsoft.Practices.Unity;
using MediaRatio = ChatterBox.Background.AppService.Dto.MediaRatio;

namespace ChatterBox.ViewModels
{
    public class SettingsViewModel : BindableBase
    {
        private const string DefaultAudioDeviceId = "ChatterBoxDefaultAudioDevice";
        private readonly ICallChannel _callChannel;
        private readonly CoreDispatcher _dispatcher;
        private readonly IForegroundUpdateService _foregroundUpdateService;

        private readonly string[] _incompatibleAudioCodecs =
        {"CN32000", "CN16000", "CN8000", "red8000", "telephone-event8000"};

        private readonly ApplicationDataContainer _localSettings;
        private readonly IMediaSettingsChannel _mediaSettings;
        private readonly NtpService _ntpService;


        private readonly Dictionary<string, CaptureCapabilities> _videoCaptureCap =
            new Dictionary<string, CaptureCapabilities>();

        private readonly string SelectedFrameRateId = nameof(SelectedFrameRateId) + "Frame";

        private ObservableCollection<CaptureCapability> _allCapFps;

        private ObservableCollection<string> _allCapRes = new ObservableCollection<string>();
        private bool _appInsightsEnabled;

        private ObservableCollection<CodecInfo> _audioCodecs;

        private ObservableCollection<MediaDevice> _audioPlayoutDevices;


        private ObservableCollection<MediaDevice> _cameras;
        private string _domain;
        private bool _etwStatsEnabled;
        private bool _statsSendToServerEnabled = false;
        private string _statsServerHost;
        private int _statsServerPort;

        private ObservableCollection<IceServerViewModel> _iceServers;

        private ObservableCollection<MediaDevice> _microphones;
        private string _ntpServerIp = "time.windows.com";
        private bool _ntpSyncEnabled;
        private bool _ntpSyncInProgress;
        private string _registeredUserName;
        private bool _rtcTraceEnabled;
        private string _rtcTraceFolderToken;

        private CodecInfo _selectedAudioCodec;

        private MediaDevice _selectedAudioPlayoutDevice;

        private MediaDevice _selectedCamera;

        private CaptureCapability _selectedCapFpsItem;

        private string _selectedCapResItem;

        private IceServerViewModel _selectedIceServer;

        private MediaDevice _selectedMicrophone;

        private CodecInfo _selectedVideoCodec;
        private string _signallingServerHost;
        private int _signallingServerPort;

        private ObservableCollection<CodecInfo> _videoCodecs;

        private bool _enableAudioDeviceSelection;

        public SettingsViewModel(IUnityContainer container,
            CoreDispatcher dispatcher,
            IForegroundUpdateService foregroundUpdateService)
        {
            _foregroundUpdateService = foregroundUpdateService;
            _localSettings = ApplicationData.Current.LocalSettings;
            _dispatcher = dispatcher;

            _mediaSettings = container.Resolve<IMediaSettingsChannel>();
            _callChannel = container.Resolve<ICallChannel>();
            _ntpService = container.Resolve<NtpService>();

            _ntpService.OnNtpSyncFailed += HandleNtpSynFailed;
            _ntpService.OnNtpTimeAvailable += HandleNtpTimeSync;

            CloseCommand = new DelegateCommand(OnCloseCommandExecute);
            SaveCommand = new DelegateCommand(OnSaveCommandExecute);
            QuitAppCommand = new DelegateCommand(OnQuitAppCommandExecute);
            DeleteIceServerCommand = new DelegateCommand<IceServerViewModel>(OnDeleteIceServerCommandExecute);
            AddIceServerCommand = new DelegateCommand(OnAddIceServerCommandExecute);

            // When AudioRoutingManager API is available, the UI elements related to microphone and speaker selection
            // are hidden from settings view.
            // In this case, the user will have the option to change the devices to be used from the call UI.
            // https://msdn.microsoft.com/en-us/library/windows/apps/windows.phone.media.devices.audioroutingmanager.aspx
            EnableAudioDeviceSelection = !ApiInformation.IsApiContractPresent("Windows.Phone.PhoneContract", 1);
    
            _foregroundUpdateService.OnMediaDevicesChanged += OnMediaDevicesChanged;
        }

        public DelegateCommand AddIceServerCommand { get; }

        /// <summary>
        ///     The list of all capture frame rates.
        /// </summary>
        public ObservableCollection<CaptureCapability> AllCapFps

        {
            get { return _allCapFps; }
            set { SetProperty(ref _allCapFps, value); }
        }

        /// <summary>
        ///     The list of all capture resolutions.
        /// </summary>
        public ObservableCollection<string> AllCapRes
        {
            get { return _allCapRes; }
            set { SetProperty(ref _allCapRes, value); }
        }

        public bool AppInsightsEnabled
        {
            get { return _appInsightsEnabled; }
            set { SetProperty(ref _appInsightsEnabled, value); }
        }

        public string ApplicationVersion
            =>
                $"ChatterBox Version: {Package.Current.Id.Version.Major}.{Package.Current.Id.Version.Minor}.{Package.Current.Id.Version.Build}.{Package.Current.Id.Version.Revision}"
            ;

        /// <summary>
        ///     The list of audio codecs.
        /// </summary>
        public ObservableCollection<CodecInfo> AudioCodecs
        {
            get { return _audioCodecs; }
            set { SetProperty(ref _audioCodecs, value); }
        }

        public ObservableCollection<MediaDevice> AudioPlayoutDevices
        {
            get { return _audioPlayoutDevices; }
            set { SetProperty(ref _audioPlayoutDevices, value); }
        }

        public ObservableCollection<MediaDevice> Cameras
        {
            get { return _cameras; }
            set { SetProperty(ref _cameras, value); }
        }

        public DelegateCommand CloseCommand { get; set; }

        public DelegateCommand<IceServerViewModel> DeleteIceServerCommand { get; }

        public string Domain
        {
            get { return _domain; }
            set { SetProperty(ref _domain, value); }
        }

        public bool EtwStatsEnabled
        {
            get { return _etwStatsEnabled; }
            set
            {
                if (!SetProperty(ref _etwStatsEnabled, value))
                {
                    return;
                }

                _mediaSettings.ToggleEtwStatsAsync(_etwStatsEnabled).AsTask();
                ETWEventLogger.Instance.ETWStatsEnabled = _etwStatsEnabled;
            }
        }

        public bool StatsSendToServerEnabled
        {
            get { return _statsSendToServerEnabled; }
            set
            {
                if (!SetProperty(ref _statsSendToServerEnabled, value))
                {
                    return;
                }
            }
        }

        public string StatsServerHost
        {
            get { return _statsServerHost; }
            set
            {
                if (!SetProperty(ref _statsServerHost, value))
                {
                    return;
                }
            }
        }

        public int StatsServerPort
        {
            get { return _statsServerPort; }
            set
            {
                if (!SetProperty(ref _statsServerPort, value))
                {
                    return;
                }
            }
        }

        public ObservableCollection<IceServerViewModel> IceServers
        {
            get { return _iceServers; }
            set { SetProperty(ref _iceServers, value); }
        }

        public ObservableCollection<MediaDevice> Microphones
        {
            get { return _microphones; }
            set { SetProperty(ref _microphones, value); }
        }

        public string NtpServerIp
        {
            get { return _ntpServerIp; }
            set { SetProperty(ref _ntpServerIp, value); }
        }

        public bool NtpSyncEnabled
        {
            get { return _ntpSyncEnabled; }
            set
            {
                if (!SetProperty(ref _ntpSyncEnabled, value))
                {
                    return;
                }

                if (_ntpSyncEnabled)
                {
                    //start ntp server sync
                    NtpSyncInProgress = true;
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    _ntpService.GetNetworkTime(NtpServerIp);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                }
                else
                {
                    //donothing
                    NtpSyncInProgress = false;
                    _ntpService.AbortSync();
                }
            }
        }

        public bool NtpSyncInProgress
        {
            get { return _ntpSyncInProgress; }
            set { SetProperty(ref _ntpSyncInProgress, value); }
        }

        public DelegateCommand QuitAppCommand { get; set; }


        public string RegisteredUserName
        {
            get { return _registeredUserName; }
            set { SetProperty(ref _registeredUserName, value); }
        }

        public bool RtcTraceEnabled
        {
            get { return _rtcTraceEnabled; }
            set
            {
                if (!SetProperty(ref _rtcTraceEnabled, value))
                {
                    return;
                }

                if (_rtcTraceEnabled)
                {
                    _mediaSettings.StartTraceAsync().AsTask();

                    if (_rtcTraceFolderToken == null)
                    {
                        var task = PickWebRtcTraceFolder();
                    }
                }
                else
                {
                    _mediaSettings.StopTraceAsync().AsTask();
                }
            }
        }

        public DelegateCommand SaveCommand { get; set; }

        public CodecInfo SelectedAudioCodec
        {
            get { return _selectedAudioCodec; }
            set { SetProperty(ref _selectedAudioCodec, value); }
        }

        public MediaDevice SelectedAudioPlayoutDevice
        {
            get { return _selectedAudioPlayoutDevice; }
            set { SetProperty(ref _selectedAudioPlayoutDevice, value); }
        }

        public MediaDevice SelectedCamera
        {
            get { return _selectedCamera; }
            set
            {
                if (SetProperty(ref _selectedCamera, value))
                {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    SetSelectedCamera();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                }
            }
        }

        /// <summary>
        ///     The selected capture frame rate.
        /// </summary>
        public CaptureCapability SelectedCapFpsItem
        {
            get { return _selectedCapFpsItem; }
            set { SetProperty(ref _selectedCapFpsItem, value); }
        }

        public string SelectedCapResItem
        {
            get { return _selectedCapResItem; }
            set
            {
                SetProperty(ref _selectedCapResItem, value);
                SetSelectedCapResItem();
            }
        }

        public IceServerViewModel SelectedIceServer
        {
            get { return _selectedIceServer; }
            set
            {
                if (_selectedIceServer != null) _selectedIceServer.IsSelected = false;
                SetProperty(ref _selectedIceServer, value);
                if (_selectedIceServer != null) _selectedIceServer.IsSelected = true;
            }
        }

        public MediaDevice SelectedMicrophone
        {
            get { return _selectedMicrophone; }
            set { SetProperty(ref _selectedMicrophone, value); }
        }

        public CodecInfo SelectedVideoCodec
        {
            get { return _selectedVideoCodec; }
            set { SetProperty(ref _selectedVideoCodec, value); }
        }

        public string SignallingServerHost
        {
            get { return _signallingServerHost; }
            set { SetProperty(ref _signallingServerHost, value); }
        }

        public int SignallingServerPort
        {
            get { return _signallingServerPort; }
            set { SetProperty(ref _signallingServerPort, value); }
        }

        /// <summary>
        ///     The list of video codecs.
        /// </summary>
        public ObservableCollection<CodecInfo> VideoCodecs
        {
            get { return _videoCodecs; }
            set { SetProperty(ref _videoCodecs, value); }
        }

        public bool EnableAudioDeviceSelection
        {
            get { return _enableAudioDeviceSelection; }
            set { SetProperty(ref _enableAudioDeviceSelection, value); }
        }

        public void Dispose()
        {
            if (_foregroundUpdateService == null) return;
            _foregroundUpdateService.OnMediaDevicesChanged -= OnMediaDevicesChanged;
        }

        public async Task<CaptureCapabilities> GetVideoCaptureCapabilitiesAsync(MediaDevice device)
        {
            if (_videoCaptureCap.ContainsKey(device.Id))
                return _videoCaptureCap[device.Id];
            var settings = new MediaCaptureInitializationSettings
            {
                VideoDeviceId = device.Id,
                MediaCategory = MediaCategory.Communications
            };
            using (var capture = new MediaCapture())
            {
                await capture.InitializeAsync(settings);
                var caps = capture.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoRecord);

                var arr = new List<CaptureCapability>();
                foreach (var cap in caps)
                {
                    if (cap.Type != "Video")
                    {
                        continue;
                    }

                    var videoCap = (VideoEncodingProperties) cap;

                    if (videoCap.FrameRate.Denominator == 0 ||
                        videoCap.FrameRate.Numerator == 0 ||
                        videoCap.Width == 0 ||
                        videoCap.Height == 0)
                    {
                        continue;
                    }
                    var captureCap = new CaptureCapability
                    {
                        Width = videoCap.Width,
                        Height = videoCap.Height,
                        FrameRate = videoCap.FrameRate.Numerator/videoCap.FrameRate.Denominator
                    };
                    captureCap.FrameRateDescription = $"{captureCap.FrameRate} fps";
                    captureCap.ResolutionDescription = $"{captureCap.Width} x {captureCap.Height}";
                    captureCap.PixelAspectRatio = new MediaRatio
                    {
                        Numerator = videoCap.PixelAspectRatio.Numerator,
                        Denominator = videoCap.PixelAspectRatio.Denominator
                    };
                    captureCap.FullDescription = $"{captureCap.ResolutionDescription} {captureCap.FrameRateDescription}";
                    arr.Add(captureCap);
                }
                _videoCaptureCap.Add(device.Id,
                    new CaptureCapabilities
                    {
                        Capabilities = arr.GroupBy(o => o.FullDescription).Select(o => o.First()).ToArray()
                    });

                return _videoCaptureCap[device.Id];
            }
        }

        public event Action OnClose;

        public async void OnNavigatedFrom()
        {
            await _mediaSettings.ReleaseDevicesAsync();
        }


        public void OnNavigatedTo()
        {
            LoadSettings();
        }

        public event Action OnQuitApp;
        public event Action OnRegistrationSettingsChanged;

        /// <summary>
        ///     Returns audio capture devices including Default option.
        /// </summary>
        private async Task<ObservableCollection<MediaDevice>> GetAudioCaptureDevicesAsync()
        {
            var audioDevices = await _mediaSettings.GetAudioCaptureDevicesAsync();
            var result = new ObservableCollection<MediaDevice>(audioDevices.Devices);
            result.Insert(0, new MediaDevice {Name = "Default", Id = DefaultAudioDeviceId, IsPreferred = false});
            return result;
        }

        /// <summary>
        ///     Returns audio playout devices including Default option.
        /// </summary>
        private async Task<ObservableCollection<MediaDevice>> GetAudioPlayoutDevicesAsync()
        {
            var audioPlayoutDevices = await _mediaSettings.GetAudioPlayoutDevicesAsync();
            var result = new ObservableCollection<MediaDevice>(audioPlayoutDevices.Devices);
            result.Insert(0, new MediaDevice {Name = "Default", Id = DefaultAudioDeviceId, IsPreferred = false});
            return result;
        }

        private void HandleNtpSynFailed()
        {
            NtpSyncInProgress = false;
        }


        private async void HandleNtpTimeSync(long ntpTime)
        {
            Debug.WriteLine($"New ntp time: {ntpTime}");
            NtpSyncInProgress = false;
            await _mediaSettings.SyncWithNtpAsync(ntpTime);
        }

        private async void LoadSettings()
        {
            await _callChannel.InitializeRtcAsync();

            SignallingServerPort = int.Parse(SignallingSettings.SignallingServerPort);
            SignallingServerHost = SignallingSettings.SignallingServerHost;
            Domain = RegistrationSettings.Domain;
            AppInsightsEnabled = SignallingSettings.AppInsightsEnabled;
            RegisteredUserName = RegistrationSettings.Name;

            if (_localSettings.Values[nameof(NtpServerIp)] != null)
            {
                NtpServerIp = (string) _localSettings.Values[nameof(NtpServerIp)];
            }

            Cameras = new ObservableCollection<MediaDevice>((await _mediaSettings.GetVideoCaptureDevicesAsync()).Devices);
            SelectedCamera = Cameras.FirstOrDefault(c => c.IsPreferred) ?? Cameras.FirstOrDefault();

            Microphones = await GetAudioCaptureDevicesAsync();
            SelectedMicrophone = Microphones.FirstOrDefault(c => c.IsPreferred);
            if (SelectedMicrophone == null)
            {
                SelectedMicrophone = Microphones.First();
                await _mediaSettings.SetAudioDeviceAsync(SelectedMicrophone.Id == DefaultAudioDeviceId
                    ? null
                    : SelectedMicrophone);
            }

            AudioPlayoutDevices = await GetAudioPlayoutDevicesAsync();
            SelectedAudioPlayoutDevice = AudioPlayoutDevices.FirstOrDefault(c => c.IsPreferred);
            if (SelectedAudioPlayoutDevice == null)
            {
                SelectedAudioPlayoutDevice = AudioPlayoutDevices.First();
                await _mediaSettings.SetAudioPlayoutDeviceAsync(SelectedAudioPlayoutDevice.Id == DefaultAudioDeviceId
                    ? null
                    : SelectedAudioPlayoutDevice);
            }

            AudioCodecs = new ObservableCollection<CodecInfo>();
            var audioCodecList = await _mediaSettings.GetAudioCodecsAsync();
            foreach (var audioCodec in audioCodecList.Codecs)
            {
                if (!_incompatibleAudioCodecs.Contains(audioCodec.Name + audioCodec.ClockRate))
                {
                    AudioCodecs.Add(audioCodec);
                }
            }
            SelectedAudioCodec = null;
            if (_localSettings.Values[nameof(SelectedAudioCodec)] != null)
            {
                var audioCodecId = (int) _localSettings.Values[nameof(SelectedAudioCodec)];
                var audioCodec = AudioCodecs.SingleOrDefault(a => a.Id.Equals(audioCodecId));
                if (audioCodec != null)
                {
                    SelectedAudioCodec = audioCodec;
                }
            }
            if (SelectedAudioCodec == null && AudioCodecs.Count > 0)
            {
                SelectedAudioCodec = AudioCodecs.First();
            }
            await _mediaSettings.SetAudioCodecAsync(SelectedAudioCodec);

            var videoCodecList = (await _mediaSettings.GetVideoCodecsAsync()).Codecs.OrderBy(codec =>
            {
                switch (codec.Name)
                {
                    case "VP8":
                        return 1;
                    case "VP9":
                        return 2;
                    case "H264":
                        return 3;
                    default:
                        return 99;
                }
            });
            VideoCodecs = new ObservableCollection<CodecInfo>(videoCodecList);
            SelectedVideoCodec = null;
            if (_localSettings.Values[nameof(SelectedVideoCodec)] != null)
            {
                var videoCodecId = (int) _localSettings.Values[nameof(SelectedVideoCodec)];
                var videoCodec = VideoCodecs.SingleOrDefault(v => v.Id.Equals(videoCodecId));
                if (videoCodec != null)
                {
                    SelectedVideoCodec = videoCodec;
                }
            }
            if (SelectedVideoCodec == null && VideoCodecs.Count > 0)
            {
                SelectedVideoCodec = VideoCodecs.First();
            }
            await _mediaSettings.SetVideoCodecAsync(SelectedVideoCodec);

            IceServers = new ObservableCollection<IceServerViewModel>(
                IceServerSettings.IceServers.Select(ices => new IceServerViewModel(ices)));

            if (_localSettings.Values[nameof(StatsServerPort)] != null)
            {
                StatsServerPort = (int)_localSettings.Values[nameof(StatsServerPort)];
            }
            else
            {
                StatsServerPort = 47005;
            }

            if (_localSettings.Values[nameof(StatsServerHost)] != null)
            {
                StatsServerHost = (string)_localSettings.Values[nameof(StatsServerHost)];
            }
            else
            {
                StatsServerHost = "localhost";
            }
        }

        private void OnAddIceServerCommandExecute()
        {
            IceServers.Insert(0, new IceServerViewModel(new IceServer()));
        }

        private void OnCloseCommandExecute()
        {
            OnClose?.Invoke();
        }

        private void OnDeleteIceServerCommandExecute(IceServerViewModel iceServerVm)
        {
            IceServers.Remove(iceServerVm);
        }

        private async void OnMediaDevicesChanged(MediaDevicesChange mediaDeviceChange)
        {
            switch (mediaDeviceChange.Type)
            {
                case MediaDeviceChangeType.VideoCapture:
                    await RefreshVideoCaptureDevices();
                    break;
                case MediaDeviceChangeType.AudioCapture:
                    await RefreshAudioCaptureDevicesAsync();
                    break;
                case MediaDeviceChangeType.AudioPlayout:
                    await RefreshAudioPlayoutDevicesAsync();
                    break;
            }
        }

        private void OnQuitAppCommandExecute()
        {
            OnQuitApp?.Invoke();
        }

        private async void OnSaveCommandExecute()
        {
            var registrationSettingChanged = false;
            if (SignallingSettings.SignallingServerPort != SignallingServerPort.ToString())
            {
                SignallingSettings.SignallingServerPort = SignallingServerPort.ToString();
                registrationSettingChanged = true;
            }

            if (SignallingSettings.SignallingServerHost != SignallingServerHost)
            {
                SignallingSettings.SignallingServerHost = SignallingServerHost;
                registrationSettingChanged = true;
            }
            if (RegistrationSettings.Domain != Domain)
            {
                RegistrationSettings.Domain = Domain;
                registrationSettingChanged = true;
            }

            if (registrationSettingChanged)
            {
                OnRegistrationSettingsChanged?.Invoke();
            }

            SignallingSettings.AppInsightsEnabled = AppInsightsEnabled;

            if (NtpServerIp != null)
            {
                _localSettings.Values[nameof(NtpServerIp)] = NtpServerIp;
            }

            if (SelectedCamera != null)
            {
                await _mediaSettings.SetVideoDeviceAsync(SelectedCamera);
            }

            if (SelectedMicrophone != null)
            {
                await
                    _mediaSettings.SetAudioDeviceAsync(SelectedMicrophone.Id == DefaultAudioDeviceId
                        ? null
                        : SelectedMicrophone);
            }

            if (SelectedAudioPlayoutDevice != null)
            {
                await _mediaSettings.SetAudioPlayoutDeviceAsync(SelectedAudioPlayoutDevice.Id == DefaultAudioDeviceId
                    ? null
                    : SelectedAudioPlayoutDevice);
            }

            if (SelectedVideoCodec != null)
            {
                await _mediaSettings.SetVideoCodecAsync(SelectedVideoCodec);
                _localSettings.Values[nameof(SelectedVideoCodec)] = SelectedVideoCodec.Id;
            }

            if (SelectedAudioCodec != null)
            {
                await _mediaSettings.SetAudioCodecAsync(SelectedAudioCodec);
                _localSettings.Values[nameof(SelectedAudioCodec)] = SelectedAudioCodec.Id;
            }

            if (SelectedCapFpsItem != null)
            {
                await
                    _mediaSettings.SetPreferredVideoCaptureFormatAsync(new VideoCaptureFormat((int) SelectedCapFpsItem.Width,
                        (int) SelectedCapFpsItem.Height,
                        (int) SelectedCapFpsItem.FrameRate));
                _localSettings.Values[nameof(SelectedCapResItem)] = SelectedCapResItem;
                _localSettings.Values[SelectedFrameRateId] = SelectedCapFpsItem?.FrameRate;
            }

            var newList =
                (from iceServerVm in IceServers where iceServerVm.Apply() select iceServerVm.IceServer).ToList();
            IceServerSettings.IceServers = newList;

            _localSettings.Values[nameof(StatsServerPort)] = StatsServerPort;
            _localSettings.Values[nameof(StatsServerHost)] = StatsServerHost;

            await _mediaSettings.SetStatsConfigAsync(new StatsConfig {
                SendStatsToServerEnabled = _statsSendToServerEnabled,
                StatsServerHost = StatsServerHost,
                StatsServerPort = StatsServerPort
            });

            OnCloseCommandExecute();
        }

        private async Task PickWebRtcTraceFolder()
        {
            var dialog = new MessageDialog("Please select the folder to save trace", "WebRTC Trace");
            dialog.Commands.Add(new UICommand("Ok"));
            await dialog.ShowAsync();
            var savePicker = new FolderPicker {SuggestedStartLocation = PickerLocationId.DocumentsLibrary};
            // Prompt user to select destination to save
            savePicker.FileTypeFilter.Add(".txt");
            var folder = await savePicker.PickSingleFolderAsync();
            if (folder != null)
            {
                await UpdateWebRtcTraceFolderTokenAsync(folder);
            }
            else
            {
                RtcTraceEnabled = false;
            }
        }

        /// <summary>
        ///     Refresh audio capture devices list.
        /// </summary>
        private async Task RefreshAudioCaptureDevicesAsync()
        {
            var selectedMicrophoneId = SelectedMicrophone?.Id;
            var oldMicrophones = Microphones;
            Microphones = await GetAudioCaptureDevicesAsync();
            var preferredMicrophone = Microphones.FirstOrDefault(c => c.IsPreferred);
            SelectedMicrophone = (selectedMicrophoneId != null
                ? Microphones.FirstOrDefault(c => c.Id == selectedMicrophoneId)
                : preferredMicrophone) ?? Microphones.First();
            if (EtwStatsEnabled)
            {
                var addedMicrophonesInfo =
                    Microphones.Where(microphone => oldMicrophones.FirstOrDefault(x => x.Id == microphone.Id) == null)
                        .Aggregate("",
                            (current, microphone) =>
                                string.Format("{0}id = {1} name = {2}\n", current, microphone.Id, microphone.Name));
                if (addedMicrophonesInfo != "")
                {
                    ETWEventLogger.Instance.LogEvent("Microphone(s) Added", addedMicrophonesInfo,
                        DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString());
                }

                var removedMicrophonesInfo =
                    oldMicrophones.Where(microphone => _microphones.FirstOrDefault(x => x.Id == microphone.Id) == null)
                        .Aggregate("",
                            (current, microphone) =>
                                string.Format("{0}id = {1} name = {2}\n", current, microphone.Id, microphone.Name));
                if (removedMicrophonesInfo != "")
                {
                    ETWEventLogger.Instance.LogEvent("Microphone(s) Removed", removedMicrophonesInfo,
                        DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString());
                }
            }
        }

        /// <summary>
        ///     Refresh audio playout devices list.
        /// </summary>
        private async Task RefreshAudioPlayoutDevicesAsync()
        {
            var selectedAudioPlayoutId = SelectedAudioPlayoutDevice?.Id;
            var oldAudioPlayoutDevices = AudioPlayoutDevices;
            AudioPlayoutDevices = await GetAudioPlayoutDevicesAsync();
            var preferredAudioPlayoutDevice = AudioPlayoutDevices.FirstOrDefault(c => c.IsPreferred);
            SelectedAudioPlayoutDevice = (selectedAudioPlayoutId != null
                ? AudioPlayoutDevices.FirstOrDefault(c => c.Id == selectedAudioPlayoutId)
                : preferredAudioPlayoutDevice) ?? AudioPlayoutDevices.First();
            if (!EtwStatsEnabled) return;
            var addedPlayoutDevicesInfo =
                AudioPlayoutDevices.Where(
                    playoutDevice => oldAudioPlayoutDevices.FirstOrDefault(x => x.Id == playoutDevice.Id) == null)
                    .Aggregate("",
                        (current, playoutDevice) =>
                            string.Format("{0}id = {1} name = {2}\n", current, playoutDevice.Id, playoutDevice.Name));
            if (addedPlayoutDevicesInfo != "")
            {
                ETWEventLogger.Instance.LogEvent("Audio Playout Device(s) Added", addedPlayoutDevicesInfo,
                    DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString());
            }

            var removedPlayoutDevicesInfo =
                oldAudioPlayoutDevices.Where(
                    playoutDevice => _audioPlayoutDevices.FirstOrDefault(x => x.Id == playoutDevice.Id) == null)
                    .Aggregate("",
                        (current, playoutDevice) =>
                            string.Format("{0}id = {1} name = {2}\n", current, playoutDevice.Id, playoutDevice.Name));
            if (removedPlayoutDevicesInfo != "")
            {
                ETWEventLogger.Instance.LogEvent("Audio Playout Device(s) Removed", removedPlayoutDevicesInfo,
                    DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString());
            }
        }

        /// <summary>
        ///     Refresh video capture devices list.
        /// </summary>
        private async Task RefreshVideoCaptureDevices()
        {
            var videoCaptureDevices = (await _mediaSettings.GetVideoCaptureDevicesAsync()).Devices;
            var removedDevicesInfo = "";
            var videoCaptureDevicesToRemove = new Collection<MediaDevice>();
            Cameras = Cameras ?? new ObservableCollection<MediaDevice>();
            foreach (
                var videoCaptureDevice in
                    Cameras.Where(
                        videoCaptureDevice =>
                            videoCaptureDevices.FirstOrDefault(x => x.Id == videoCaptureDevice.Id) == null))
            {
                videoCaptureDevicesToRemove.Add(videoCaptureDevice);
                if (EtwStatsEnabled)
                {
                    removedDevicesInfo += "id = " + videoCaptureDevice.Id + " name = " +
                                          videoCaptureDevice.Name + "\n";
                }
            }
            foreach (var removedVideoCaptureDevices in videoCaptureDevicesToRemove)
            {
                if (SelectedCamera != null && SelectedCamera.Id == removedVideoCaptureDevices.Id)
                {
                    SelectedCamera = null;
                }
                Cameras.Remove(removedVideoCaptureDevices);
            }
            if (removedDevicesInfo != "")
            {
                ETWEventLogger.Instance.LogEvent("Video Device(s) Removed", removedDevicesInfo,
                    DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString());
            }
            var addedDevicesInfo = "";
            foreach (var videoCaptureDevice in videoCaptureDevices)
            {
                if (Cameras.FirstOrDefault(x => x.Id == videoCaptureDevice.Id) == null)
                {
                    Cameras.Add(videoCaptureDevice);
                    if (EtwStatsEnabled)
                    {
                        addedDevicesInfo += "id = " + videoCaptureDevice.Id + " name = " +
                                            videoCaptureDevice.Name + "\n";
                    }
                }
            }
            if (addedDevicesInfo != "")
            {
                ETWEventLogger.Instance.LogEvent("Video Device(s) Added", addedDevicesInfo,
                    DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString());
            }
            if (SelectedCamera == null)
            {
                SelectedCamera = Cameras.FirstOrDefault();
            }
        }


        private async Task SetSelectedCamera()
        {
            var capRes = new List<string>();
            CaptureCapabilities captureCapabilities;

            if (SelectedCamera == null) return;

            try
            {
                captureCapabilities = await GetVideoCaptureCapabilitiesAsync(SelectedCamera);
            }
            catch (Exception ex)
            {
                while (ex is AggregateException && ex.InnerException != null)
                    ex = ex.InnerException;
                var errorMsg = "SetSelectedCamera: Failed to GetVideoCaptureCapabilities (Error: " + ex.Message + ")";
                Debug.WriteLine(errorMsg);
                var msgDialog = new MessageDialog(errorMsg);
                await msgDialog.ShowAsync();
                return;
            }
            if (captureCapabilities == null)
            {
                var errorMsg = "SetSelectedCamera: Failed to GetVideoCaptureCapabilities (Result is null)";
                Debug.WriteLine(errorMsg);
                var msgDialog = new MessageDialog(errorMsg);
                await msgDialog.ShowAsync();
                return;
            }

            var uniqueRes = captureCapabilities.Capabilities.GroupBy(test => test.ResolutionDescription)
                .Select(grp => grp.FirstOrDefault()).ToList();
            CaptureCapability defaultResolution = null;
            foreach (var resolution in uniqueRes)
            {
                if (defaultResolution == null)
                {
                    defaultResolution = resolution;
                }
                capRes.Add(resolution.ResolutionDescription);
                if ((resolution.Width == 640) && (resolution.Height == 480))
                {
                    defaultResolution = resolution;
                }
            }
            var selectedCapResItem = string.Empty;

            if (_localSettings.Values[nameof(SelectedCapResItem)] != null)
            {
                selectedCapResItem = (string) _localSettings.Values[nameof(SelectedCapResItem)];
            }

            AllCapRes = new ObservableCollection<string>(capRes);
            if (!string.IsNullOrEmpty(selectedCapResItem) && AllCapRes.Contains(selectedCapResItem))
            {
                SelectedCapResItem = selectedCapResItem;
            }
            else
            {
                if (defaultResolution != null) SelectedCapResItem = defaultResolution.ResolutionDescription;
            }
        }

        private void SetSelectedCapResItem()
        {
            var opCap = GetVideoCaptureCapabilitiesAsync(SelectedCamera);
            opCap.ContinueWith(caps =>
            {
                var fpsList = from cap in caps.Result.Capabilities
                    where cap.ResolutionDescription == SelectedCapResItem
                    select cap;
                var t = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    async () =>
                    {
                        CaptureCapability defaultFps = null;
                        uint selectedCapFpsFrameRate = 0;
                        if (_localSettings.Values[SelectedFrameRateId] != null)
                        {
                            selectedCapFpsFrameRate = (uint) _localSettings.Values[SelectedFrameRateId];
                        }
                        if (AllCapFps == null)
                        {
                            AllCapFps = new ObservableCollection<CaptureCapability>();
                        }
                        else
                        {
                            AllCapFps.Clear();
                        }
                        foreach (var fps in fpsList)
                        {
                            if (selectedCapFpsFrameRate != 0 && fps.FrameRate == selectedCapFpsFrameRate)
                            {
                                defaultFps = fps;
                            }
                            AllCapFps.Add(fps);
                            if (defaultFps == null)
                            {
                                defaultFps = fps;
                            }
                        }
                        SelectedCapFpsItem = defaultFps;
                        if (SelectedCapFpsItem == null) return;
                        await _mediaSettings.SetPreferredVideoCaptureFormatAsync(
                            new VideoCaptureFormat((int) SelectedCapFpsItem.Width,
                                (int) SelectedCapFpsItem.Height,
                                (int) SelectedCapFpsItem.FrameRate));
                    });
                var uiTask = _dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    () => { OnPropertyChanged(nameof(AllCapFps)); });
            });
        }

        private async Task UpdateWebRtcTraceFolderTokenAsync(StorageFolder aFolder)
        {
            _rtcTraceFolderToken = StorageApplicationPermissions.FutureAccessList.Add(aFolder);
            await _mediaSettings.SetTraceFolderTokenAsync(_rtcTraceFolderToken);
        }
    }
}