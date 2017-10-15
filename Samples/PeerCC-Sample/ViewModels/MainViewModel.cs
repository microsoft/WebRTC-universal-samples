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
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.Storage;
using Windows.System.Display;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Microsoft.HockeyApp;
using PeerConnectionClient.Model;
using PeerConnectionClient.MVVM;
using PeerConnectionClient.Signaling;
using PeerConnectionClient.Utilities;
#if ORTCLIB
using Org.Ortc;
using Org.Ortc.Adapter;
using PeerConnectionClient.Ortc;
using PeerConnectionClient.Ortc.Utilities;
using CodecInfo = Org.Ortc.RTCRtpCodecCapability;
using MediaVideoTrack = Org.Ortc.MediaStreamTrack;
using MediaAudioTrack = Org.Ortc.MediaStreamTrack;
using FrameCounterHelper= PeerConnectionClient.Ortc.OrtcStatsManager;
#else
using Org.WebRtc;
#endif


namespace PeerConnectionClient.ViewModels
{
    public delegate void InitializedDelegate();
    internal class MainViewModel : DispatcherBindableBase
    {
        public event InitializedDelegate OnInitialized;

        /// <summary>
        /// Constructor for MainViewModel.
        /// </summary>
        /// <param name="uiDispatcher">Core event message dispatcher.</param>
        public MainViewModel(CoreDispatcher uiDispatcher)
            : base(uiDispatcher)
        {
            // Initialize all the action commands
            ConnectCommand = new ActionCommand(ConnectCommandExecute, ConnectCommandCanExecute);
            ConnectToPeerCommand = new ActionCommand(ConnectToPeerCommandExecute, ConnectToPeerCommandCanExecute);
            DisconnectFromPeerCommand = new ActionCommand(DisconnectFromPeerCommandExecute, DisconnectFromPeerCommandCanExecute);
            DisconnectFromServerCommand = new ActionCommand(DisconnectFromServerExecute, DisconnectFromServerCanExecute);
            AddIceServerCommand = new ActionCommand(AddIceServerExecute, AddIceServerCanExecute);
            RemoveSelectedIceServerCommand = new ActionCommand(RemoveSelectedIceServerExecute, RemoveSelectedIceServerCanExecute);
            SendFeedbackCommand = new ActionCommand(SendFeedbackExecute);
            SettingsButtonCommand = new ActionCommand(SettingsButtonExecute);

            // Configure application version string format
            var version = Windows.ApplicationModel.Package.Current.Id.Version;
            AppVersion = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";

            IsReadyToConnect = true;
            _settingsButtonChecked = false;
            ScrollBarVisibilityType = ScrollBarVisibility.Auto;

            _ntpService = new NtpService(uiDispatcher);
            _ntpService.OnNTPSyncFailed += HandleNtpSynFailed;
            _ntpService.OnNTPTimeAvailable += HandleNtpTimeSync;

            // Prepare Hockey app to collect the crash logs and send to the server
            LoadHockeyAppSettings();

            // Display a permission dialog to request access to the microphone and camera
            WebRTC.RequestAccessForMediaCapture().AsTask().ContinueWith(antecedent =>
            {
                if (antecedent.Result)
                {
                    Initialize(uiDispatcher);
                }
                else
                {
                    RunOnUiThread(async () =>
                    {
                        var msgDialog = new MessageDialog(
                            "Failed to obtain access to multimedia devices!");
                        await msgDialog.ShowAsync();
                    });
                }
            });

        }

        /// <summary>
        /// Media Failed event handler for remote/peer video.
        /// Invoked when an error occurs in peer media source.
        /// </summary>
        /// <param name="sender">The object where the handler is attached.</param>
        /// <param name="e">Details about the exception routed event.</param>
        public void PeerVideo_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
          Debug.WriteLine("PeerVideo_MediaFailed");
          if (_peerVideoTrack != null)
          {
            Debug.WriteLine("Re-establishing peer video");

            Conductor.Instance.Media.AddVideoTrackMediaElementPair(_peerVideoTrack, PeerVideo, "PEER");
            //var source = Media.CreateMedia().CreateMediaSource(_peerVideoTrack, "PEER");
            //RunOnUiThread(() =>
            //{
            //  PeerVideo.SetMediaStreamSource(source);
            //  Debug.WriteLine("Peer video re-established");
            //});

            }
        }

        /// <summary>
        /// Media Failed event handler for the self video.
        /// Invoked when an error occurs in the self media source.
        /// </summary>
        /// <param name="sender">The object where the handler is attached.</param>
        /// <param name="e">Details about the exception routed event.</param>
        public void SelfVideo_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
          Debug.WriteLine("SelfVideo_MediaFailed");
          if (_selfVideoTrack != null && VideoLoopbackEnabled)
          {
            Debug.WriteLine("Re-establishing self video");

                Conductor.Instance.Media.AddVideoTrackMediaElementPair(_selfVideoTrack, SelfVideo, "SELF");
                //var source = Media.CreateMedia().CreateMediaSource(_selfVideoTrack, "SELF");
                //RunOnUiThread(() =>
                //{
                //    SelfVideo.SetMediaStreamSource(source);
                //    Debug.WriteLine("Self video re-established");
                //});

            }
        }

        // Help to make sure the screen is not locked while on call
        readonly DisplayRequest _keepScreenOnRequest = new DisplayRequest();
        private bool _keepOnScreenRequested;

        private MediaVideoTrack _peerVideoTrack;
        private MediaVideoTrack _selfVideoTrack;

        private readonly NtpService _ntpService;


        /// <summary>
        /// The initializer for MainViewModel.
        /// </summary>
        /// <param name="uiDispatcher">The UI dispatcher.</param>
        public void Initialize(CoreDispatcher uiDispatcher)
        {
            WebRTC.Initialize(uiDispatcher);
            var settings = ApplicationData.Current.LocalSettings;

            // Get information of cameras attached to the device
            Cameras = new ObservableCollection<MediaDevice>();
            string savedVideoRecordingDeviceId = null;
            if (settings.Values["SelectedCameraId"] != null)
            {
                savedVideoRecordingDeviceId = (string)settings.Values["SelectedCameraId"];
            }
            // Get information of microphones attached to the device
            Microphones = new ObservableCollection<MediaDevice>();
            string savedAudioRecordingDeviceId = null;
            if (settings.Values["SelectedMicrophoneId"] != null)
            {
                savedAudioRecordingDeviceId = (string)settings.Values["SelectedMicrophoneId"];
            }
            AudioPlayoutDevices = new ObservableCollection<MediaDevice>();
            string savedAudioPlayoutDeviceId = null;
            if (settings.Values["SelectedAudioPlayoutDeviceId"] != null)
            {
                savedAudioPlayoutDeviceId = (string)settings.Values["SelectedAudioPlayoutDeviceId"];
            }
#if ORTCLIB
            MediaDevices.EnumerateDevices().AsTask().ContinueWith(devices =>
            {
                foreach (MediaDeviceInfo devInfo in devices.Result)
                {
                    MediaDevice mediaDevice = Helper.ToMediaDevice(devInfo);
                    RunOnUiThread(() =>
                    {
                        switch (devInfo.Kind)
                        {
                            case MediaDeviceKind.AudioInput:
                                if (savedAudioRecordingDeviceId != null && savedAudioRecordingDeviceId == devInfo.DeviceId)
                                {
                                    SelectedMicrophone = mediaDevice;
                                }
                                Microphones.Add(mediaDevice);
                                break;
                            case MediaDeviceKind.AudioOutput:
                                if (savedAudioPlayoutDeviceId != null && savedAudioPlayoutDeviceId == devInfo.DeviceId)
                                {
                                    SelectedAudioPlayoutDevice = mediaDevice;
                                }
                                AudioPlayoutDevices.Add(mediaDevice);
                                break;
                            case MediaDeviceKind.VideoInput:

                                if (savedVideoRecordingDeviceId != null && savedVideoRecordingDeviceId == devInfo.DeviceId)
                                {
                                    SelectedCamera = mediaDevice;
                                }
                                Cameras.Add(mediaDevice);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    });
                }

                RunOnUiThread(() =>
                {
                    if (SelectedCamera == null && Cameras.Count > 0)
                    {
                        SelectedCamera = Cameras.First();
                    }
                    if (SelectedMicrophone == null && Microphones.Count > 0)
                    {
                        SelectedMicrophone = Microphones.First();
                    }
                    if (SelectedAudioPlayoutDevice == null && AudioPlayoutDevices.Count > 0)
                    {
                        SelectedAudioPlayoutDevice = AudioPlayoutDevices.First();
                    }
                });
            });
            MediaDevices.Singleton.OnDeviceChange += OnMediaDevicesChanged;
#else
            foreach (MediaDevice videoCaptureDevice in Conductor.Instance.Media.GetVideoCaptureDevices())
            {
                if (savedVideoRecordingDeviceId != null && savedVideoRecordingDeviceId == videoCaptureDevice.Id)
                {
                    SelectedCamera = videoCaptureDevice;
                }
                Cameras.Add(videoCaptureDevice);
            }

            if (SelectedCamera == null && Cameras.Count > 0)
            {
                SelectedCamera = Cameras.First();
            }
            Conductor.Instance.Media.OnMediaDevicesChanged += OnMediaDevicesChanged;
#endif

            // Handler for Peer/Self video frame rate changed event
            FrameCounterHelper.FramesPerSecondChanged += (id, frameRate) =>
            {
                RunOnUiThread(() =>
                {
                    if (id == "SELF")
                    {
                        SelfVideoFps = frameRate;
                    }
                    else if (id == "PEER")
                    {
                        PeerVideoFps = frameRate;
                    }
                });
            };

            // Handler for Peer/Self video resolution changed event 
            ResolutionHelper.ResolutionChanged += (id, width, height) =>
            {
                RunOnUiThread(() =>
                {
                    if (id == "SELF")
                    {
                        SelfHeight = height.ToString();
                        SelfWidth = width.ToString();
                    }
                    else if (id == "PEER")
                    {
                        PeerHeight = height.ToString();
                        PeerWidth = width.ToString();
                    }
                });
            };

            // A Peer is connected to the server event handler
            Conductor.Instance.Signaler.OnPeerConnected += (peerId, peerName) =>
            {
                RunOnUiThread(() =>
                {
                    if (Peers == null)
                    {
                        Peers = new ObservableCollection<Peer>();
                        Conductor.Instance.Peers = Peers;
                    }
                    Peers.Add(new Peer { Id = peerId, Name = peerName });
                });
            };

            // A Peer is disconnected from the server event handler
            Conductor.Instance.Signaler.OnPeerDisconnected += peerId =>
            {
                RunOnUiThread(() =>
                {
                    var peerToRemove = Peers?.FirstOrDefault(p => p.Id == peerId);
                    if (peerToRemove != null)
                        Peers.Remove(peerToRemove);
                });
            };

            // The user is Signed in to the server event handler
            Conductor.Instance.Signaler.OnSignedIn += () =>
            {
                RunOnUiThread(() =>
                {
                    IsConnected = true;
                    IsMicrophoneEnabled = true;
                    IsCameraEnabled = true;
                    IsConnecting = false;
                });
            };

            // Failed to connect to the server event handler
            Conductor.Instance.Signaler.OnServerConnectionFailure += () =>
            {
                RunOnUiThread(async () =>
                {
                    IsConnecting = false;
                    MessageDialog msgDialog = new MessageDialog("Failed to connect to server!");
                    await msgDialog.ShowAsync();
                });
            };

            // The current user is disconnected from the server event handler
            Conductor.Instance.Signaler.OnDisconnected += () =>
            {
                RunOnUiThread(() =>
                {
                    IsConnected = false;
                    IsMicrophoneEnabled = false;
                    IsCameraEnabled = false;
                    IsDisconnecting = false;
                    Peers?.Clear();
                });
            };

            // Event handlers for managing the media streams 
#if ORTCLIB
            Conductor.Instance.OnAddRemoteTrack += Conductor_OnAddRemoteTrack;
            Conductor.Instance.OnRemoveTrack += Conductor_OnRemoveTrack;
            Conductor.Instance.OnAddLocalStream += Conductor_OnAddLocalStream;
#else
            Conductor.Instance.OnAddRemoteStream += Conductor_OnAddRemoteStream;
            Conductor.Instance.OnRemoveRemoteStream += Conductor_OnRemoveRemoteStream;
            Conductor.Instance.OnAddLocalStream += Conductor_OnAddLocalStream;

            Conductor.Instance.OnConnectionHealthStats += Conductor_OnPeerConnectionHealthStats;
#endif

            //PlotlyManager.UpdateUploadingStatsState += PlotlyManager_OnUpdatedUploadingStatsState;
            //PlotlyManager.OnError += PlotlyManager_OnError;
            // Connected to a peer event handler
            Conductor.Instance.OnPeerConnectionCreated += () =>
            {
                RunOnUiThread(() =>
                {
                    IsReadyToConnect = false;
                    IsConnectedToPeer = true;
                    if (SettingsButtonChecked)
                    {
                        // close settings screen if open
                        SettingsButtonChecked = false;
                        ScrollBarVisibilityType = ScrollBarVisibility.Disabled;
                    }
                    IsReadyToDisconnect = false;
                    if (SettingsButtonChecked)
                    {
                        // close settings screen if open
                        SettingsButtonChecked = false;
                        ScrollBarVisibilityType = ScrollBarVisibility.Disabled;
                    }

                    // Make sure the screen is always active while on call
                    if (!_keepOnScreenRequested)
                    {
                        _keepScreenOnRequest.RequestActive();
                        _keepOnScreenRequested = true;
                    }

                    UpdateScrollBarVisibilityTypeHelper();
                });
            };

            // Connection between the current user and a peer is closed event handler
            Conductor.Instance.OnPeerConnectionClosed += () =>
            {
                RunOnUiThread(() =>
                {
                    IsConnectedToPeer = false;
                    Conductor.Instance.Media.RemoveVideoTrackMediaElementPair(_peerVideoTrack);
                    //PeerVideo.Source = null;

                    Conductor.Instance.Media.RemoveVideoTrackMediaElementPair(_selfVideoTrack);
                    //SelfVideo.Stop();
                    //SelfVideo.ClearValue(MediaElement.SourceProperty);
                    //SelfVideo.Source = null;

                    _peerVideoTrack = null;
                    _selfVideoTrack = null;
                    GC.Collect(); // Ensure all references are truly dropped.
                    IsMicrophoneEnabled = true;
                    IsCameraEnabled = true;
                    SelfVideoFps = PeerVideoFps = "";

                    // Make sure to allow the screen to be locked after the call
                    if (_keepOnScreenRequested)
                    {
                        _keepScreenOnRequest.RequestRelease();
                        _keepOnScreenRequested = false;
                    }
                    UpdateScrollBarVisibilityTypeHelper();
                });
            };

            // Ready to connect to the server event handler
            Conductor.Instance.OnReadyToConnect += () => { RunOnUiThread(() => { IsReadyToConnect = true; }); };

            // Initialize the Ice servers list
            IceServers = new ObservableCollection<IceServer>();
            NewIceServer = new IceServer();

            // Prepare to list supported audio codecs
            AudioCodecs = new ObservableCollection<CodecInfo>();
            var audioCodecList = WebRTC.GetAudioCodecs();

            // These are features added to existing codecs, they can't decode/encode real audio data so ignore them
            string[] incompatibleAudioCodecs = new string[] { "CN32000", "CN16000", "CN8000", "red8000", "telephone-event8000" };

            // Prepare to list supported video codecs
            VideoCodecs = new ObservableCollection<CodecInfo>();

            // Order the video codecs so that the stable VP8 is in front.
            var videoCodecList = WebRTC.GetVideoCodecs().OrderBy(codec =>
            {
                switch (codec.Name)
                {
                    case "VP8": return 1;
                    case "VP9": return 2;
                    case "H264": return 3;
                    default: return 99;
                }
            });

            // Load the supported audio/video information into the Settings controls
            RunOnUiThread(() =>
            {
                foreach (var audioCodec in audioCodecList)
                {
#if ORTCLIB
                    if (!incompatibleAudioCodecs.Contains(audioCodec.Name + audioCodec.ClockRate))
#else
                    if (!incompatibleAudioCodecs.Contains(audioCodec.Name + audioCodec.ClockRate))
#endif
                    {
                        AudioCodecs.Add(audioCodec);
                    }
                }

                if (AudioCodecs.Count > 0)
                {
                    if (settings.Values["SelectedAudioCodecId"] != null)
                    {
#if ORTCLIB
                        byte id = Convert.ToByte(settings.Values["SelectedAudioCodecId"]);
#else
                        int id = Convert.ToInt32(settings.Values["SelectedAudioCodecId"]);
#endif
                        foreach (var audioCodec in AudioCodecs)
                        {
#if ORTCLIB
                            byte audioCodecId = audioCodec.PreferredPayloadType;
#else
                            int audioCodecId = audioCodec.Id;
#endif
                            if (audioCodecId == id)
                            {
                                SelectedAudioCodec = audioCodec;
                                break;
                            }
                        }
                    }
                    if (SelectedAudioCodec == null)
                    {
                        SelectedAudioCodec = AudioCodecs.First();
                    }
                }

                foreach (var videoCodec in videoCodecList)
                {
                    VideoCodecs.Add(videoCodec);
                }

                if (VideoCodecs.Count > 0)
                {
                    if (settings.Values["SelectedVideoCodecId"] != null)
                    {
#if ORTCLIB
                        byte id = Convert.ToByte(settings.Values["SelectedVideoCodecId"]);
#else
                        int id = Convert.ToInt32(settings.Values["SelectedVideoCodecId"]);
#endif
                        foreach (var videoCodec in VideoCodecs)
                        {
#if ORTCLIB
                            byte videoCodecId = videoCodec.PreferredPayloadType;
#else
                            int videoCodecId = videoCodec.Id;
#endif
                            if (videoCodecId == id)
                            {
                                SelectedVideoCodec = videoCodec;
                                break;
                            }
                        }
                    }
                    if (SelectedVideoCodec == null)
                    {
                        SelectedVideoCodec = VideoCodecs.First();
                    }
                }
            });
            LoadSettings();
            RunOnUiThread(() =>
            {
                OnInitialized?.Invoke();
            });
        }

#if ORTCLIB
        /// <summary>
        /// Handle media devices change event triggered by WebRTC.
        /// </summary>
        private async void OnMediaDevicesChanged()
        {
            IList<MediaDeviceInfo> devices = await MediaDevices.EnumerateDevices();
            //contentAsync.AsTask().Wait();
            //var devices = contentAsync.GetResults();

            Collection<MediaDevice> audioInputDevices = new Collection<MediaDevice>();
            Collection<MediaDevice> audioOutputDevices = new Collection<MediaDevice>();
            Collection<MediaDevice> videoInputDevices = new Collection<MediaDevice>();
            foreach (MediaDeviceInfo devInfo in devices)
            {
                MediaDevice mediaDevice = Helper.ToMediaDevice(devInfo);
                switch (devInfo.Kind)
                {
                    case MediaDeviceKind.AudioInput:
                        audioInputDevices.Add(mediaDevice);
                        break;
                    case MediaDeviceKind.AudioOutput:
                        audioOutputDevices.Add(mediaDevice);
                        break;
                    case MediaDeviceKind.VideoInput:
                        videoInputDevices.Add(mediaDevice);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            RefreshVideoCaptureDevices(videoInputDevices);

            RefreshAudioCaptureDevices(audioInputDevices);

            RefreshAudioPlayoutDevices(audioOutputDevices);

        }
#else
        /// <summary>
        /// Handle media devices change event triggered by WebRTC.
        /// </summary>
        /// <param name="mediaType">The type of devices changed</param>
        private void OnMediaDevicesChanged(MediaDeviceType mediaType)
        {
            switch(mediaType)
            {
                case MediaDeviceType.MediaDeviceType_VideoCapture:
                    RefreshVideoCaptureDevices(Conductor.Instance.Media.GetVideoCaptureDevices());
                    break;
            }
        }
#endif
        /// <summary>
        /// Refresh video capture devices list.
        /// </summary>
        private void RefreshVideoCaptureDevices(IList<MediaDevice> videoCaptureDevices)
        {
            RunOnUiThread(() => {
                Collection<MediaDevice> videoCaptureDevicesToRemove = new Collection<MediaDevice>();
                foreach (MediaDevice videoCaptureDevice in Cameras)
                {
                    if (videoCaptureDevices.FirstOrDefault(x => x.Id == videoCaptureDevice.Id) == null)
                    {
                        videoCaptureDevicesToRemove.Add(videoCaptureDevice);
                    }
                }
                foreach (MediaDevice removedVideoCaptureDevices in videoCaptureDevicesToRemove)
                {
                    if (SelectedCamera != null && SelectedCamera.Id == removedVideoCaptureDevices.Id)
                    {
                        SelectedCamera = null;
                    }
                    Cameras.Remove(removedVideoCaptureDevices);
                }
                foreach (MediaDevice videoCaptureDevice in videoCaptureDevices)
                {
                    if (Cameras.FirstOrDefault(x => x.Id == videoCaptureDevice.Id) == null)
                    {
                        Cameras.Add(videoCaptureDevice);
                    }
                }

                if (SelectedCamera == null)
                {
                    SelectedCamera = Cameras.FirstOrDefault();
                }
            });
        }

        /// <summary>
        /// Refresh audio capture devices list.
        /// </summary>
        private void RefreshAudioCaptureDevices(IList<MediaDevice> audioCaptureDevices)
        {
            RunOnUiThread(() => {
                var selectedMicrophoneId = SelectedMicrophone?.Id;
                SelectedMicrophone = null;
                Microphones.Clear();
                foreach (MediaDevice audioCaptureDevice in audioCaptureDevices)
                {
                    Microphones.Add(audioCaptureDevice);
                    if (audioCaptureDevice.Id == selectedMicrophoneId)
                    {
                        SelectedMicrophone = Microphones.Last();
                    }
                }
                if (SelectedMicrophone == null)
                {
                    SelectedMicrophone = Microphones.FirstOrDefault();
                }

                if (SelectedMicrophone == null)
                {
                    SelectedMicrophone = Microphones.FirstOrDefault();
                }
            });
        }

        /// <summary>
        /// Refresh audio playout devices list.
        /// </summary>
        private void RefreshAudioPlayoutDevices(IList<MediaDevice> audioPlayoutDevices)
        {
            RunOnUiThread(() => {
                var selectedPlayoutDeviceId = SelectedAudioPlayoutDevice?.Id;
                SelectedAudioPlayoutDevice = null;
                AudioPlayoutDevices.Clear();
                foreach (MediaDevice audioPlayoutDevice in audioPlayoutDevices)
                {
                    AudioPlayoutDevices.Add(audioPlayoutDevice);
                    if (audioPlayoutDevice.Id == selectedPlayoutDeviceId)
                    {
                        SelectedAudioPlayoutDevice = audioPlayoutDevice;
                    }
                }
                if (SelectedAudioPlayoutDevice == null)
                {
                    SelectedAudioPlayoutDevice = AudioPlayoutDevices.FirstOrDefault();
                }
            });
        }

#if ORTCLIB
        /// <summary>
        /// Add remote track event handler.
        /// </summary>
        /// <param name="evt">Details about Media track event.</param>
        private void Conductor_OnAddRemoteTrack(RTCTrackEvent evt)
        {
            _peerVideoTrack = evt.Track;
            if (evt.Track != null && evt.Track.Kind == MediaStreamTrackKind.Video)
            {
                Conductor.Instance.Media.AddVideoTrackMediaElementPair(_peerVideoTrack, PeerVideo, "PEER");
                //var source = Media.CreateMedia().CreateMediaSource(_peerVideoTrack, "PEER");
                //RunOnUiThread(() => { PeerVideo.SetMediaStreamSource(source); });
            }

            IsReadyToDisconnect = true;
        }

        /// <summary>
        /// Remove remote track event handler.
        /// </summary>
        /// <param name="evt">Details about Media track event.</param>
        private void Conductor_OnRemoveTrack(RTCTrackEvent evt)
        {
        }
#else
        /// <summary>
        /// Add remote stream event handler.
        /// </summary>
        /// <param name="evt">Details about Media stream event.</param>
        private void Conductor_OnAddRemoteStream(MediaStreamEvent evt)
        {
            _peerVideoTrack = evt.Stream.GetVideoTracks().FirstOrDefault();
            if (_peerVideoTrack != null)
            {
                Conductor.Instance.Media.AddVideoTrackMediaElementPair(_peerVideoTrack, PeerVideo, "PEER");
                //var source = Media.CreateMedia().CreateMediaSource(_peerVideoTrack, "PEER");
                //RunOnUiThread(() =>
                //{
                //  PeerVideo.SetMediaStreamSource(source);
                //});
            }

            IsReadyToDisconnect = true;
        }

        /// <summary>
        /// Remove remote stream event handler.
        /// </summary>
        /// <param name="evt">Details about Media stream event.</param>
        private void Conductor_OnRemoveRemoteStream(MediaStreamEvent evt)
        {
            RunOnUiThread(() =>
            {
                Conductor.Instance.Media.RemoveVideoTrackMediaElementPair(_peerVideoTrack);
                //PeerVideo.SetMediaStreamSource(null);
            });
        }
#endif

        /// <summary>
        /// Add local stream event handler.
        /// </summary>
        /// <param name="evt">Details about Media stream event.</param>
        private void Conductor_OnAddLocalStream(MediaStreamEvent evt)
        {
          _selfVideoTrack = evt.Stream.GetVideoTracks().FirstOrDefault();
          if (_selfVideoTrack != null)
            {
                //var source = Media.CreateMedia().CreateMediaSource(_selfVideoTrack, "SELF");
                RunOnUiThread(() =>
                    {
                        if (_cameraEnabled)
                        {
                            Conductor.Instance.EnableLocalVideoStream();
                        }
                        else
                        {
                            Conductor.Instance.DisableLocalVideoStream();
                        }

                        if (_microphoneIsOn)
                        {
                            Conductor.Instance.UnmuteMicrophone();
                        }
                        else
                        {
                            Conductor.Instance.MuteMicrophone();
                        }
                    });
                if (VideoLoopbackEnabled)
                {
                    Conductor.Instance.Media.AddVideoTrackMediaElementPair(_selfVideoTrack, SelfVideo, "SELF");
                }
            }
        }
#if !ORTCLIB
        /// <summary>
        /// New connection health statistics received.
        /// </summary>
        /// <param name="stats">Connection health statistics.</param>
        private void Conductor_OnPeerConnectionHealthStats(RTCPeerConnectionHealthStats stats)
        {
            PeerConnectionHealthStats = stats; 
        }
#endif

#if PLOTLY
        private void PlotlyManager_OnUpdatedUploadingStatsState(bool uploading)
        {
            IsUploadingStatsInProgress = uploading;
        }

        private void PlotlyManager_OnError(string error)
        {
            RunOnUiThread(async ()=>
            {
                var messageDialog = new MessageDialog(error);

                messageDialog.Commands.Add(new UICommand("Close"));

                messageDialog.DefaultCommandIndex = 0;
                messageDialog.CancelCommandIndex = 1;
                await messageDialog.ShowAsync();
            });
        }
#endif
                    #region Bindings

        private ValidableNonEmptyString _ntpServer;

        /// <summary>
        /// Address of the ntp server to sync app clock.
        /// </summary>
        public ValidableNonEmptyString NtpServer
        {
            get { return _ntpServer; }
            set
            {
                SetProperty(ref _ntpServer, value);
                _ntpServer.PropertyChanged += NtpServer_PropertyChanged;
                NtpSyncEnabled = false; //reset
            }
        }


        private ValidableNonEmptyString _ip;

        /// <summary>
        /// IP address of the server to connect.
        /// </summary>
        public ValidableNonEmptyString Ip
        {
            get { return _ip; }
            set
            {
                SetProperty(ref _ip, value);
                _ip.PropertyChanged += Ip_PropertyChanged;
            }
        }

        private ValidableIntegerString _port;

        /// <summary>
        /// The port used to connect to the server.
        /// </summary>
        public ValidableIntegerString Port
        {
            get { return _port; }
            set
            {
                SetProperty(ref _port, value);
                _port.PropertyChanged += Port_PropertyChanged;
            }
        }

        private ObservableCollection<Peer> _peers;

        /// <summary>
        /// The list of connected peers.
        /// </summary>
        public ObservableCollection<Peer> Peers
        {
            get { return _peers; }
            set { SetProperty(ref _peers, value); }
        }

        private Peer _selectedPeer;

        /// <summary>
        /// The selected peer's info.
        /// </summary>
        public Peer SelectedPeer
        {
            get { return _selectedPeer; }
            set
            {
                SetProperty(ref _selectedPeer, value);
                ConnectToPeerCommand.RaiseCanExecuteChanged();
            }
        }

        private ActionCommand _connectCommand;

        /// <summary>
        /// Command to connect to the server.
        /// </summary>
        public ActionCommand ConnectCommand
        {
            get { return _connectCommand; }
            set { SetProperty(ref _connectCommand, value); }
        }

        private ActionCommand _connectToPeerCommand;

        /// <summary>
        /// Command to connect to a peer.
        /// </summary>
        public ActionCommand ConnectToPeerCommand
        {
            get { return _connectToPeerCommand; }
            set { SetProperty(ref _connectToPeerCommand, value); }
        }

        private ActionCommand _disconnectFromPeerCommand;

        /// <summary>
        /// Command to disconnect from a peer.
        /// </summary>
        public ActionCommand DisconnectFromPeerCommand
        {
            get { return _disconnectFromPeerCommand; }
            set { SetProperty(ref _disconnectFromPeerCommand, value); }
        }

        private ActionCommand _disconnectFromServerCommand;

        /// <summary>
        /// Command to disconnect from the server.
        /// </summary>
        public ActionCommand DisconnectFromServerCommand
        {
            get { return _disconnectFromServerCommand; }
            set { SetProperty(ref _disconnectFromServerCommand, value); }
        }

        private ActionCommand _addIceServerCommand;

        /// <summary>
        /// Command to add a new Ice server to the list.
        /// </summary>
        public ActionCommand AddIceServerCommand
        {
            get { return _addIceServerCommand; }
            set { SetProperty(ref _addIceServerCommand, value); }
        }

        private ActionCommand _removeSelectedIceServerCommand;

        /// <summary>
        /// Command to remove an Ice server from the list.
        /// </summary>
        public ActionCommand RemoveSelectedIceServerCommand
        {
            get { return _removeSelectedIceServerCommand; }
            set { SetProperty(ref _removeSelectedIceServerCommand, value); }
        }

        private ActionCommand _settingsButtonCommand;

        /// <summary>
        /// Command to open/hide the Settings controls.
        /// </summary>
        public ActionCommand SettingsButtonCommand
        {
            get { return _settingsButtonCommand; }
            set { SetProperty(ref _settingsButtonCommand, value); }
        }

        private String _peerWidth;

        /// <summary>
        /// Peer video width.
        /// </summary>
        public String PeerWidth
        {
            get { return _peerWidth; }
            set { SetProperty(ref _peerWidth, value); }
        }

        private String _peerHeight;

        /// <summary>
        /// Peer video height.
        /// </summary>
        public String PeerHeight
        {
            get { return _peerHeight; }
            set { SetProperty(ref _peerHeight, value); }
        }

        private String _selfWidth;

        /// <summary>
        /// Self video width.
        /// </summary>
        public String SelfWidth
        {
            get { return _selfWidth; }
            set { SetProperty(ref _selfWidth, value); }
        }

        private String _selfHeight;

        /// <summary>
        /// Self video height.
        /// </summary>
        public String SelfHeight
        {
            get { return _selfHeight; }
            set { SetProperty(ref _selfHeight, value); }
        }

        private String _peerVideoFps;

        /// <summary>
        /// Frame rate per second for the peer's video.
        /// </summary>
        public String PeerVideoFps
        {
            get { return _peerVideoFps; }
            set { SetProperty(ref _peerVideoFps, value); }
        }

        private String _selfVideoFps;

        /// <summary>
        /// Frame rate per second for the self video.
        /// </summary>
        public String SelfVideoFps
        {
            get { return _selfVideoFps; }
            set { SetProperty(ref _selfVideoFps, value); }
        }

        private ActionCommand _sendFeedbackCommand;

        /// <summary>
        /// Command to send feedback to the specified email account.
        /// </summary>
        public ActionCommand SendFeedbackCommand
        {
            get { return _sendFeedbackCommand; }
            set { SetProperty(ref _sendFeedbackCommand, value); }
        }

        private bool _hasServer;

        /// <summary>
        /// Indicator if a server IP address is specified in Settings.
        /// </summary>
        public bool HasServer
        {
            get { return _hasServer; }
            set { SetProperty(ref _hasServer, value); }
        }

        private bool _isConnected;

        /// <summary>
        /// Indicator if the user is connected to the server.
        /// </summary>
        public bool IsConnected
        {
            get { return _isConnected; }
            set
            {
                SetProperty(ref _isConnected, value);
                ConnectCommand.RaiseCanExecuteChanged();
                DisconnectFromServerCommand.RaiseCanExecuteChanged();
            }
        }

        private bool _isConnecting;

        /// <summary>
        /// Indicator if the application is in the process of connecting to the server.
        /// </summary>
        public bool IsConnecting
        {
            get { return _isConnecting; }
            set
            {
                SetProperty(ref _isConnecting, value);
                ConnectCommand.RaiseCanExecuteChanged();
            }
        }

        private bool _isDisconnecting;

        /// <summary>
        /// Indicator if the application is in the process of disconnecting from the server.
        /// </summary>
        public bool IsDisconnecting
        {
            get { return _isDisconnecting; }
            set
            {
                SetProperty(ref _isDisconnecting, value);
                DisconnectFromServerCommand.RaiseCanExecuteChanged();
            }
        }

        private bool _isConnectedToPeer;

        /// <summary>
        /// Indicator if the user is connected to a peer.
        /// </summary>
        public bool IsConnectedToPeer
        {
            get { return _isConnectedToPeer; }
            set
            {
                SetProperty(ref _isConnectedToPeer, value);
                ConnectToPeerCommand.RaiseCanExecuteChanged();
                DisconnectFromPeerCommand.RaiseCanExecuteChanged();

                PeerConnectionHealthStats = null;
                UpdatePeerConnHealthStatsVisibilityHelper();
                UpdateLoopbackVideoVisibilityHelper();
            }
        }

        private bool _isReadyToConnect;

        /// <summary>
        /// Indicator if the app is ready to connect to a peer.
        /// </summary>
        public bool IsReadyToConnect
        {
            get { return _isReadyToConnect; }
            set
            {
                SetProperty(ref _isReadyToConnect, value);
                ConnectToPeerCommand.RaiseCanExecuteChanged();
                DisconnectFromPeerCommand.RaiseCanExecuteChanged();
            }
        }

        private bool _isReadyToDisconnect;

        /// <summary>
        /// Indicator if the app is ready to disconnect from a peer.
        /// </summary>
        public bool IsReadyToDisconnect
        {
            get { return _isReadyToDisconnect; }
            set
            {
                SetProperty(ref _isReadyToDisconnect, value);
                DisconnectFromPeerCommand.RaiseCanExecuteChanged();
            }
        }


        private ScrollBarVisibility _scrollBarVisibility;

        /// <summary>
        /// The scroll bar visibility type.
        /// This is used to have a scrollable UI if the application 
        /// main page is bigger in size than the device screen.
        /// </summary>
        public ScrollBarVisibility ScrollBarVisibilityType
        {
            get { return _scrollBarVisibility; }
            set { SetProperty(ref _scrollBarVisibility, value); }
        }

        private bool _cameraEnabled = true;

        /// <summary>
        /// Camera on/off toggle button.
        /// Disabled/Enabled local stream if the camera is off/on.
        /// </summary>
        public bool CameraEnabled
        {
            get { return _cameraEnabled; }
            set
            {
                if (!SetProperty(ref _cameraEnabled, value))
                {
                    return;
                }

                if (IsConnectedToPeer)
                {
                    if (_cameraEnabled)
                    {
                        Conductor.Instance.EnableLocalVideoStream();
                    }
                    else
                    {
                        Conductor.Instance.DisableLocalVideoStream();
                    }
                }
            }
        }

        private bool _microphoneIsOn = true;

        /// <summary>
        /// Microphone on/off toggle button.
        /// Unmute/Mute audio if the microphone is off/on.
        /// </summary>
        public bool MicrophoneIsOn
        {
            get { return _microphoneIsOn; }
            set
            {
                if (!SetProperty(ref _microphoneIsOn, value))
                {
                    return;
                }

                if (IsConnectedToPeer)
                {
                    if (_microphoneIsOn)
                    {
                        Conductor.Instance.UnmuteMicrophone();
                    }
                    else
                    {
                        Conductor.Instance.MuteMicrophone();
                    }
                }
            }
        }

        private bool _isMicrophoneEnabled = true;

        /// <summary>
        /// Indicator if the microphone is enabled.
        /// </summary>
        public bool IsMicrophoneEnabled
        {
            get { return _isMicrophoneEnabled; }
            set { SetProperty(ref _isMicrophoneEnabled, value); }
        }

        private bool _isCameraEnabled = true;

        /// <summary>
        /// Indicator if the camera is enabled.
        /// </summary>
        public bool IsCameraEnabled
        {
            get { return _isCameraEnabled; }
            set { SetProperty(ref _isCameraEnabled, value); }
        }

        private bool _tracingEnabled;

        /// <summary>
        /// Enable tracing toggle button.
        /// Stop tracing and send logs/Start tracing if the tracing is disabled/enabled.
        /// </summary>
        public bool TracingEnabled
        {
            get { return _tracingEnabled; }
            set
            {
                if (!SetProperty(ref _tracingEnabled, value))
                {
                    return;
                }
#if ORTCLIB
                if (_tracingEnabled)
                {
                    Org.Ortc.Ortc.StartMediaTracing();
                }
                else
                {
                    Org.Ortc.Ortc.StopMediaTracing();
                    Org.Ortc.Ortc.SaveMediaTrace(_traceServerIp, Int32.Parse(_traceServerPort));
                }
#else
                if (_tracingEnabled)
                {
                    WebRTC.StartTracing("webrtc-trace.txt");
                }
                else
                {
                    WebRTC.StopTracing();
                }
#endif
                AppPerformanceCheck();
            }
        }

        private string _traceServerIp = string.Empty;

        /// <summary>
        /// The trace server IP address.
        /// </summary>
        public string TraceServerIp
        {
            get { return _traceServerIp; }
            set
            {
                if (!SetProperty(ref _traceServerIp, value))
                {
                    return;
                }
                var localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values["TraceServerIp"] = _traceServerIp;
            }
        }

        private string _traceServerPort = string.Empty;

        /// <summary>
        /// The trace server port to connect.
        /// </summary>
        public string TraceServerPort
        {
            get { return _traceServerPort; }
            set
            {
                if (!SetProperty(ref _traceServerPort, value)) return;
                var localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values["TraceServerPort"] = _traceServerPort;
            }
        }

        private ObservableCollection<MediaDevice> _cameras;

        /// <summary>
        /// The list of available cameras.
        /// </summary>
        public ObservableCollection<MediaDevice> Cameras
        {
            get { return _cameras; }
            set { SetProperty(ref _cameras, value); }
        }

        private MediaDevice _selectedCamera;

        /// <summary>
        /// The selected camera.
        /// </summary>
        public MediaDevice SelectedCamera
        {
            get { return _selectedCamera; }
            set
            {
                SetProperty(ref _selectedCamera, value);

                if (value == null)
                {
                    return;
                }

                var localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values["SelectedCameraId"] = _selectedCamera.Id;
                Conductor.Instance.Media.SelectVideoDevice(_selectedCamera);
                if (_allCapRes == null)
                {
                    _allCapRes = new ObservableCollection<String>();
                }
                else
                {
                    _allCapRes.Clear();
                }

                var opRes = value.GetVideoCaptureCapabilities();
                opRes.AsTask().ContinueWith(resolutions =>
                {
                    RunOnUiThread(async () =>
                    {
                        if (resolutions.IsFaulted)
                        {
                            Exception ex = resolutions.Exception;
                            while (ex is AggregateException && ex.InnerException != null)
                                ex = ex.InnerException;
                            String errorMsg = "SetSelectedCamera: Failed to GetVideoCaptureCapabilities (Error: " + ex.Message + ")";
                            Debug.WriteLine("[Error] " + errorMsg);
                            var msgDialog = new MessageDialog(errorMsg);
                            await msgDialog.ShowAsync();
                            return;
                        }
                        if (resolutions.Result == null)
                        {
                            String errorMsg = "SetSelectedCamera: Failed to GetVideoCaptureCapabilities (Result is null)";
                            Debug.WriteLine("[Error] " + errorMsg);
                            var msgDialog = new MessageDialog(errorMsg);
                            await msgDialog.ShowAsync();
                            return;
                        }
                        var uniqueRes = resolutions.Result.GroupBy(test => test.ResolutionDescription).Select(grp => grp.First()).ToList();
                        CaptureCapability defaultResolution = null;
                        foreach (var resolution in uniqueRes)
                        {
                            if (defaultResolution == null)
                            {
                                defaultResolution = resolution;
                            }
                            _allCapRes.Add(resolution.ResolutionDescription);
                            if ((resolution.Width == 640) && (resolution.Height == 480))
                            {
                                defaultResolution = resolution;
                            }
                        }
                        var settings = ApplicationData.Current.LocalSettings;
                        string selectedCapResItem = string.Empty;

                        if (settings.Values["SelectedCapResItem"] != null)
                        {
                            selectedCapResItem = (string) settings.Values["SelectedCapResItem"];
                        }

                        if (!string.IsNullOrEmpty(selectedCapResItem) && _allCapRes.Contains(selectedCapResItem))
                        {
                            SelectedCapResItem = selectedCapResItem;
                        }
                        else
                        {
                            SelectedCapResItem = defaultResolution?.ResolutionDescription;
                        }
                    });
                    OnPropertyChanged("AllCapRes");
                });
            }
        }

        private ObservableCollection<MediaDevice> _microphones;

        /// <summary>
        /// The list of available microphones.
        /// </summary>
        public ObservableCollection<MediaDevice> Microphones
        {
            get { return _microphones; }
            set { SetProperty(ref _microphones, value); }
        }

        private MediaDevice _selectedMicrophone;

        /// <summary>
        /// The selected microphone.
        /// </summary>
        public MediaDevice SelectedMicrophone
        {
            get { return _selectedMicrophone; }
            set
            {
                if (SetProperty(ref _selectedMicrophone, value) && value != null)
                {
                    var localSettings = ApplicationData.Current.LocalSettings;
                    localSettings.Values["SelectedMicrophoneId"] = _selectedMicrophone.Id;
                }
            }
        }

        private ObservableCollection<MediaDevice> _audioPlayoutDevices;

        /// <summary>
        /// The list of available audio playout devices.
        /// </summary>
        public ObservableCollection<MediaDevice> AudioPlayoutDevices
        {
            get { return _audioPlayoutDevices; }
            set { SetProperty(ref _audioPlayoutDevices, value); }
        }

        private MediaDevice _selectedAudioPlayoutDevice;

        /// <summary>
        /// The selected audio playout device.
        /// </summary>
        public MediaDevice SelectedAudioPlayoutDevice
        {
            get { return _selectedAudioPlayoutDevice; }
            set
            {
                if (SetProperty(ref _selectedAudioPlayoutDevice, value) && value != null)
                {
                    var localSettings = ApplicationData.Current.LocalSettings;
                    localSettings.Values["SelectedAudioPlayoutDeviceId"] = _selectedAudioPlayoutDevice.Id;
                }
            }
        }

        private bool _loggingEnabled;

        /// <summary>
        /// Indicator if logging is enabled.
        /// </summary>
        public bool LoggingEnabled
        {
            get { return _loggingEnabled; }
            set
            {
                if (!SetProperty(ref _loggingEnabled, value))
                {
                    return;
                }

                if (_loggingEnabled)
                {
#if ORTCLIB
                    Logger.InstallTelnetLogger(UInt16.Parse(_traceServerPort), 60, true);
                    Logger.SetLogLevel(Org.Ortc.Log.Level.Debug);
                    Logger.SetLogLevel(Org.Ortc.Log.Component.OrtcLibWebrtc, Org.Ortc.Log.Level.Detail);
                    var message = "ORTC logging enabled, connect to TCP port " + _traceServerPort +
                                  " to receive log stream.";
#else
                    WebRTC.EnableLogging(LogLevel.LOGLVL_INFO);
                    var message = "WebRTC logging enabled, connect to TCP port 47003 to receive log stream.";
#endif
                    var msgDialog = new MessageDialog(message);
                    var asyncOp = msgDialog.ShowAsync();
                }
                else
                {
#if ORTCLIB
                    Logger.UninstallTelnetLogger();
#else
                    WebRTC.DisableLogging();
#endif
                    var task = SavingLogging();
                }
            }
        }

        private bool _videoLoopbackEnabled = true;

        /// <summary>
        /// Video loopback indicator/control.
        /// </summary>
        public bool VideoLoopbackEnabled
        {
            get { return _videoLoopbackEnabled; }
            set
            {
                if (SetProperty(ref _videoLoopbackEnabled, value))
                {
                    if (value)
                    {
                        if (_selfVideoTrack != null)
                        {
                            Debug.WriteLine("Enabling video loopback");

                            Conductor.Instance.Media.AddVideoTrackMediaElementPair(_selfVideoTrack, SelfVideo, "SELF");
                            //var source = Media.CreateMedia().CreateMediaSource(_selfVideoTrack, "SELF");
                            //RunOnUiThread(() =>
                            //{
                            //    SelfVideo.SetMediaStreamSource(source);
                            //    Debug.WriteLine("Video loopback enabled");
                            //});
                        }
                    }
                    else
                    {
                        // This is a hack/workaround for destroying the internal stream source (RTMediaStreamSource)
                        // instance inside webrtc winuwp api when loopback is disabled.
                        // For some reason, the RTMediaStreamSource instance is not destroyed when only SelfVideo.Source
                        // is set to null.
                        // For unknown reasons, when executing the above sequence (set to null, stop, set to null), the
                        // internal stream source is destroyed.
                        // Apparently, with webrtc package version < 1.1.175, the internal stream source was destroyed
                        // corectly, only by setting SelfVideo.Source to null.
                        Conductor.Instance.Media.RemoveVideoTrackMediaElementPair(_selfVideoTrack);
                        //SelfVideo.Source = null;
                        //SelfVideo.Stop();
                        //SelfVideo.Source = null;
                        GC.Collect(); // Ensure all references are truly dropped.
                    }
                }
                UpdateLoopbackVideoVisibilityHelper();
            }
        }

        /// <summary>
        /// Saves the logs to a file in a selected directory.
        /// </summary>
        private async Task SavingLogging()
        {
#if ORTCLIB
            StorageFolder logFolder = WebRTC.LogFolder();
            String logFileName = WebRTC.LogFileName();
#else
            StorageFolder logFolder = WebRTC.LogFolder;
            String logFileName = WebRTC.LogFileName;
#endif

            StorageFile logFile = await logFolder.GetFileAsync(logFileName);

            WebrtcLoggingFile = null; // Reset

            if (logFile != null)
            {
                Windows.Storage.Pickers.FileSavePicker savePicker = new Windows.Storage.Pickers.FileSavePicker
                {
                    SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
                };

                // Generate log file with timestamp
                DateTime now = DateTime.Now;
                object[] args = {now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second};
                String targetFileName = string.Format("webrt_logging_{0}{1}{2}{3}{4}{5}", args);
                savePicker.SuggestedFileName = targetFileName;

                savePicker.FileTypeChoices.Add("webrtc log", new List<string>() {".log"});
                
                // Prompt user to select destination to save
                StorageFile targetFile = await savePicker.PickSaveFileAsync();

                var task = saveLogFileToUserSelectedFile(logFile, targetFile);
            }
        }

        /// <summary>
        /// Helper to save the log file .
        /// </summary>
        /// <param name="source">The log source file</param>
        /// <param name="targetFile">The target file</param>
        /// <returns></returns>
        async Task saveLogFileToUserSelectedFile(StorageFile source, StorageFile targetFile)
        {
            if (targetFile != null)
            {
                await source.CopyAndReplaceAsync(targetFile);
            }
        }

        private ObservableCollection<IceServer> _iceServers;

        /// <summary>
        /// The list of Ice servers.
        /// </summary>
        public ObservableCollection<IceServer> IceServers
        {
            get { return _iceServers; }
            set { SetProperty(ref _iceServers, value); }
        }

        private IceServer _selectedIceServer;

        /// <summary>
        /// The selected Ice server.
        /// </summary>
        public IceServer SelectedIceServer
        {
            get { return _selectedIceServer; }
            set
            {
                SetProperty(ref _selectedIceServer, value);
                RemoveSelectedIceServerCommand.RaiseCanExecuteChanged();
            }
        }

        private IceServer _newIceServer;

        /// <summary>
        /// New Ice server, invokes the NewIceServer event.
        /// </summary>
        public IceServer NewIceServer
        {
            get { return _newIceServer; }
            set
            {
                if (SetProperty(ref _newIceServer, value))
                {
                    _newIceServer.PropertyChanged += NewIceServer_PropertyChanged;
                }
            }
        }

        private ObservableCollection<CodecInfo> _audioCodecs;

        /// <summary>
        /// The list of audio codecs.
        /// </summary>
        public ObservableCollection<CodecInfo> AudioCodecs
        {
            get { return _audioCodecs; }
            set { SetProperty(ref _audioCodecs, value); }
        }

        /// <summary>
        /// The selected Audio codec.
        /// </summary>
        public CodecInfo SelectedAudioCodec
        {
            get { return Conductor.Instance.AudioCodec; }
            set
            {
                if (Conductor.Instance.AudioCodec == value)
                {
                    return;
                }
                Conductor.Instance.AudioCodec = value;
                OnPropertyChanged(() => SelectedAudioCodec);
                var localSettings = ApplicationData.Current.LocalSettings;
#if ORTCLIB
                localSettings.Values["SelectedAudioCodecId"] = Conductor.Instance.AudioCodec.PreferredPayloadType;
#else
                localSettings.Values["SelectedAudioCodecId"] = Conductor.Instance.AudioCodec.Id;
#endif
            }
        }

        private ObservableCollection<String> _allCapRes;

        /// <summary>
        /// The list of all capture resolutions.
        /// </summary>
        public ObservableCollection<String> AllCapRes
        {
            get { return _allCapRes ?? (_allCapRes = new ObservableCollection<String>()); }
            set { SetProperty(ref _allCapRes, value); }
        }

        private String _selectedCapResItem;

        /// <summary>
        /// The selected capture resolution.
        /// </summary>
        public String SelectedCapResItem
        {
            get { return _selectedCapResItem; }
            set
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values["SelectedCapResItem"] = value;

                if (AllCapFps == null)
                {
                    AllCapFps = new ObservableCollection<CaptureCapability>();
                }
                else
                {
                    AllCapFps.Clear();
                }
                var opCap = SelectedCamera.GetVideoCaptureCapabilities();
                opCap.AsTask().ContinueWith(caps =>
                {
                    var fpsList = from cap in caps.Result where cap.ResolutionDescription == value select cap;
                    var t = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            CaptureCapability defaultFps = null;
                            uint selectedCapFpsFrameRate = 0;
                            var settings = ApplicationData.Current.LocalSettings;
                            if (settings.Values["SelectedCapFPSItemFrameRate"] != null)
                            {
                            selectedCapFpsFrameRate = (uint) settings.Values["SelectedCapFPSItemFrameRate"];
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
                        });
                    OnPropertyChanged("AllCapFps");
                });
                SetProperty(ref _selectedCapResItem, value);
            }
        }

        private ObservableCollection<CaptureCapability> _allCapFps;

        /// <summary>
        /// The list of all capture frame rates.
        /// </summary>
        public ObservableCollection<CaptureCapability> AllCapFps
        {
            get { return _allCapFps ?? (_allCapFps = new ObservableCollection<CaptureCapability>()); }
            set { SetProperty(ref _allCapFps, value); }
        }

        private CaptureCapability _selectedCapFpsItem;

        /// <summary>
        /// The selected capture frame rate.
        /// </summary>
        public CaptureCapability SelectedCapFpsItem
        {
            get { return _selectedCapFpsItem; }
            set
            {
                if (SetProperty(ref _selectedCapFpsItem, value))
                {
                    Conductor.Instance.VideoCaptureProfile = value;
                    Conductor.Instance.UpdatePreferredFrameFormat();

                    var localSettings = ApplicationData.Current.LocalSettings;
                    localSettings.Values["SelectedCapFPSItemFrameRate"] = value?.FrameRate ?? 0;
                }
            }
        }

        private ObservableCollection<CodecInfo> _videoCodecs;

        /// <summary>
        /// The list of video codecs.
        /// </summary>
        public ObservableCollection<CodecInfo> VideoCodecs
        {
            get { return _videoCodecs; }
            set { SetProperty(ref _videoCodecs, value); }
        }

        /// <summary>
        /// The selected video codec.
        /// </summary>
        public CodecInfo SelectedVideoCodec
        {
            get { return Conductor.Instance.VideoCodec; }
            set
            {
                if (Conductor.Instance.VideoCodec == value)
                {
                    return;
                }

                Conductor.Instance.VideoCodec = value;
                OnPropertyChanged(() => SelectedVideoCodec);
                var localSettings = ApplicationData.Current.LocalSettings;
#if ORTCLIB
                localSettings.Values["SelectedVideoCodecId"] = Conductor.Instance.VideoCodec.PreferredPayloadType;
#else
                localSettings.Values["SelectedVideoCodecId"] = Conductor.Instance.VideoCodec.Id;
#endif
            }
        }

        private string _appVersion = "N/A";

        /// <summary>
        /// The application version.
        /// </summary>
        public string AppVersion
        {
            get { return _appVersion; }
            set { SetProperty(ref _appVersion, value); }
        }

        private string _crashReportUserInfo = "";

        /// <summary>
        /// The user info to provide when a crash happens.
        /// </summary>
        public string CrashReportUserInfo
        {
            get { return _crashReportUserInfo; }
            set
            {
                if (SetProperty(ref _crashReportUserInfo, value))
                {
                    var localSettings = ApplicationData.Current.LocalSettings;
                    localSettings.Values["CrashReportUserInfo"] = _crashReportUserInfo;
                    HockeyClient.Current.UpdateContactInfo(_crashReportUserInfo, "");
                }
            }
        }

        /// <summary>
        /// Enable/Disable ETW stats used by WebRTCDiagHubTool Visual Studio plugin.
        /// If the ETW Stats are disabled, no data will be sent to the plugin.
        /// </summary>
        public bool EtwStatsEnabled
        {
            get { return Conductor.Instance.EtwStatsEnabled; }
            set
            {
                if (Conductor.Instance.EtwStatsEnabled != value)
                {
                    Conductor.Instance.EtwStatsEnabled = value;
                    OnPropertyChanged("EtwStatsEnabled");
                }

#if ORTCLIB
                if (value)
                {
                    Logger.InstallEventingListener("", 0, 60);
                }
                else
                {
                    Logger.UninstallEventingListener();
                }
#endif

                AppPerformanceCheck();
            }
        }

        /// <summary>
        /// Peer connection health statistics from WebRTC.
        /// </summary>
        private RTCPeerConnectionHealthStats _peerConnectionHealthStats;

        public RTCPeerConnectionHealthStats PeerConnectionHealthStats
        {
            get { return _peerConnectionHealthStats; }
            set
            {
                if (SetProperty(ref _peerConnectionHealthStats, value))
                {
                    UpdatePeerConnHealthStatsVisibilityHelper();
                }
            }
        }

        /// <summary>
        /// Enable/Disable peer connection health stats.
        /// </summary>
        private bool _peerConnectioneHealthStatsEnabled;

        public bool PeerConnectionHealthStatsEnabled
        {
            get { return _peerConnectioneHealthStatsEnabled; }
            set
            {
                if (SetProperty(ref _peerConnectioneHealthStatsEnabled, value))
                {
                    Conductor.Instance.PeerConnectionStatsEnabled = value;
                    UpdatePeerConnHealthStatsVisibilityHelper();
                }
            }
        }

        /// <summary>
        /// Flag for showing/hiding the peer connection health stats.
        /// </summary>
        private bool _showPeerConnectionHealthStats;

        public bool ShowPeerConnectionHealthStats
        {
            get { return _showPeerConnectionHealthStats; }
            set { SetProperty(ref _showPeerConnectionHealthStats, value); }
        }

        /// <summary>
        /// Flag for showing/hiding the loopback video UI element
        /// </summary>
        private bool _showLoopbackVideo;

        public bool ShowLoopbackVideo
        {
            get { return _showLoopbackVideo; }
            set { SetProperty(ref _showLoopbackVideo, value); }
        }

        public MediaElement SelfVideo;
        public MediaElement PeerVideo;

#endregion

        /// <summary>
        /// Logic to determine if the server is configured.
        /// </summary>
        private void ReevaluateHasServer()
        {
            HasServer = Ip != null && Ip.Valid && Port != null && Port.Valid;
        }

        /// <summary>
        /// Logic to determine if the application is ready to connect to a server.
        /// </summary>
        /// <param name="obj">The sender object.</param>
        /// <returns>True if the application is ready to connect to server.</returns>
        private bool ConnectCommandCanExecute(object obj)
        {
            return !IsConnected && !IsConnecting && Ip.Valid && Port.Valid;
        }

        /// <summary>
        /// Executer command for connecting to server.
        /// </summary>
        /// <param name="obj">The sender object.</param>
        private void ConnectCommandExecute(object obj)
        {
            new Task(() =>
            {
                IsConnecting = true;
                Conductor.Instance.StartLogin(Ip.Value, Port.Value);
            }).Start();
        }

        /// <summary>
        /// Logic to determine if the application is ready to connect to a peer.
        /// </summary>
        /// <param name="obj">The sender object.</param>
        /// <returns>True if the application is ready to connect to a peer.</returns>
        private bool ConnectToPeerCommandCanExecute(object obj)
        {
            return SelectedPeer != null && Peers.Contains(SelectedPeer) && !IsConnectedToPeer && IsReadyToConnect;
        }

        /// <summary>
        /// Executer command to connect to a peer.
        /// </summary>
        /// <param name="obj"></param>
        private void ConnectToPeerCommandExecute(object obj)
        {
            new Task(() => { Conductor.Instance.ConnectToPeer(SelectedPeer); }).Start();
        }

        /// <summary>
        /// Logic to determine if the application is ready to disconnect from peer.
        /// </summary>
        /// <param name="obj">The sender object.</param>
        /// <returns>True if the application is ready to disconnect from a peer.</returns>
        private bool DisconnectFromPeerCommandCanExecute(object obj)
        {
            return IsConnectedToPeer && IsReadyToDisconnect;
        }

        /// <summary>
        /// Executer command to disconnect from a peer.
        /// </summary>
        /// <param name="obj">The sender object.</param>
        private void DisconnectFromPeerCommandExecute(object obj)
        {
            new Task(() => { var task = Conductor.Instance.DisconnectFromPeer(); }).Start();
        }

        /// <summary>
        /// Logic to determine if the application is ready to disconnect from server.
        /// </summary>
        /// <param name="obj">The sender object.</param>
        /// <returns>True if the application is ready to disconnect from the server.</returns>
        private bool DisconnectFromServerCanExecute(object obj)
        {
            if (IsDisconnecting)
            {
                return false;
            }

            return IsConnected;
        }

        /// <summary>
        /// Executer command to disconnect from server.
        /// </summary>
        /// <param name="obj"></param>
        private void DisconnectFromServerExecute(object obj)
        {
            new Task(() =>
            {
                IsDisconnecting = true;
                var task = Conductor.Instance.DisconnectFromServer();
            }).Start();

            Peers?.Clear();
        }

        /// <summary>
        /// Logic to determine if the application is ready to add an Ice server.
        /// </summary>
        /// <param name="obj">The sender object.</param>
        /// <returns>True if the application can add an Ice server to the list.</returns>
        private bool AddIceServerCanExecute(object obj)
        {
            return NewIceServer.Valid;
        }

        /// <summary>
        /// Executer command to add an Ice server.
        /// </summary>
        /// <param name="obj">The sender object.</param>
        private void AddIceServerExecute(object obj)
        {
            IceServers.Add(_newIceServer);
            OnPropertyChanged(() => IceServers);
            Conductor.Instance.ConfigureIceServers(IceServers);
            SaveIceServerList();
            NewIceServer = new IceServer();
        }

        /// <summary>
        /// Logic to determine if the application is ready to remove an Ice server.
        /// </summary>
        /// <param name="obj">The sender object.</param>
        /// <returns>True if the application can remove an ice server from the list.</returns>
        private bool RemoveSelectedIceServerCanExecute(object obj)
        {
            return SelectedIceServer != null;
        }

        /// <summary>
        /// Executer command to remove an Ice server.
        /// </summary>
        /// <param name="obj">The sender object.</param>
        private void RemoveSelectedIceServerExecute(object obj)
        {
            IceServers.Remove(_selectedIceServer);
            OnPropertyChanged(() => IceServers);
            SaveIceServerList();
            Conductor.Instance.ConfigureIceServers(IceServers);
        }

        /// <summary>
        /// Executer command to send feedback.
        /// </summary>
        /// <param name="obj">The sender object.</param>
        private void SendFeedbackExecute(object obj)
        {
/*#if !WINDOWS_UAP // Disable on Win10 for now.
            HockeyClient.Current.ShowFeedback();
#endif*/
            }

    private bool _settingsButtonChecked;

        /// <summary>
        /// Indicator if Settings button is checked
        /// </summary>
        public bool SettingsButtonChecked
        {
            get
            {
                return _settingsButtonChecked;
            }
            set
            {
                SetProperty(ref _settingsButtonChecked, value);
            }
        }

        /// <summary>
        /// Execute for Settings button is hit event.
        /// Calls to update the ScrollBarVisibilityType property.
        /// </summary>
        /// <param name="obj">The sender object.</param>
        private void SettingsButtonExecute(object obj)
        {
            UpdateScrollBarVisibilityTypeHelper();
        }

        /// <summary>
        /// Makes the UI scrollable if the controls do not fit the device
        /// screen size.
        /// The UI is not scrollable if connected to a peer.
        /// </summary>
        private void UpdateScrollBarVisibilityTypeHelper()
        {
            if (SettingsButtonChecked)
            {
                ScrollBarVisibilityType = ScrollBarVisibility.Auto;
            }
            else if (IsConnectedToPeer)
            {
                ScrollBarVisibilityType = ScrollBarVisibility.Disabled;
            }
            else
            {
                ScrollBarVisibilityType = ScrollBarVisibility.Auto;
            }
        }

        /// <summary>
        /// Loads the settings with predefined and default values.
        /// </summary>
        void LoadSettings()
        {
            var settings = ApplicationData.Current.LocalSettings;

            // Default values:
            var configTraceServerIp = "127.0.0.1";
            var configTraceServerPort = "55000";
            var peerCcServerIp = new ValidableNonEmptyString("127.0.0.1");
            var ntpServerAddress = new ValidableNonEmptyString("time.windows.com");
            var peerCcPortInt = 8888;

            if (settings.Values["PeerCCServerIp"] != null)
            {
                peerCcServerIp = new ValidableNonEmptyString((string) settings.Values["PeerCCServerIp"]);
            }

            if (settings.Values["PeerCCServerPort"] != null)
            {
                peerCcPortInt = Convert.ToInt32(settings.Values["PeerCCServerPort"]);
            }

            var configIceServers = new ObservableCollection<IceServer>();

            if (settings.Values["TraceServerIp"] != null)
            {
                configTraceServerIp = (string) settings.Values["TraceServerIp"];
            }

            if (settings.Values["TraceServerPort"] != null)
            {
                configTraceServerPort = (string) settings.Values["TraceServerPort"];
            }

            bool useDefaultIceServers = true;
            if (settings.Values["IceServerList"] != null)
            {
                try
                {
                    configIceServers = XmlSerializer<ObservableCollection<IceServer>>.FromXml((string) settings.Values["IceServerList"]);
                    useDefaultIceServers = false;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[Error] Failed to load IceServer from config, using defaults (ex=" + ex.Message + ")");
                }
            }
            if (useDefaultIceServers)
            {
                // Default values:
                configIceServers.Clear();
                configIceServers.Add(new IceServer("stun.l.google.com:19302", IceServer.ServerType.STUN));
                configIceServers.Add(new IceServer("stun1.l.google.com:19302", IceServer.ServerType.STUN));
                configIceServers.Add(new IceServer("stun2.l.google.com:19302", IceServer.ServerType.STUN));
                configIceServers.Add(new IceServer("stun3.l.google.com:19302", IceServer.ServerType.STUN));
                configIceServers.Add(new IceServer("stun4.l.google.com:19302", IceServer.ServerType.STUN));
            }

            if (settings.Values["NTPServer"] != null && (string)settings.Values["NTPServer"] !="" )
            {
                ntpServerAddress = new ValidableNonEmptyString((string)settings.Values["NTPServer"]);
            }

            RunOnUiThread(() =>
            {
                IceServers = configIceServers;
                TraceServerIp = configTraceServerIp;
                TraceServerPort = configTraceServerPort;
                Ip = peerCcServerIp;
                NtpServer = ntpServerAddress;
                Port = new ValidableIntegerString(peerCcPortInt, 0, 65535);
                ReevaluateHasServer();
            });

            Conductor.Instance.ConfigureIceServers(configIceServers);
        }

        /// <summary>
        /// Loads the Hockey app settings.
        /// </summary>
        void LoadHockeyAppSettings()
        {
            var settings = ApplicationData.Current.LocalSettings;

            // Default values:
            var configCrashReportUserInfo = "";

            if (settings.Values["CrashReportUserInfo"] != null)
            {
                configCrashReportUserInfo = (string) settings.Values["CrashReportUserInfo"];
            }

            if (configCrashReportUserInfo == "")
            {
                var hostname = NetworkInformation.GetHostNames().FirstOrDefault(h => h.Type == HostNameType.DomainName);
                configCrashReportUserInfo = hostname?.CanonicalName ?? "<unknown host>";
            }

            RunOnUiThread(() => { CrashReportUserInfo = configCrashReportUserInfo; });
        }

        /// <summary>
        /// Saves the Ice servers list.
        /// </summary>
        void SaveIceServerList()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            string xmlIceServers = XmlSerializer<ObservableCollection<IceServer>>.ToXml(IceServers);
            localSettings.Values["IceServerList"] = xmlIceServers;
        }

        /// <summary>
        /// NewIceServer event handler .
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="e">Property Changed event information.</param>
        void NewIceServer_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Valid")
            {
                AddIceServerCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// ntp server changed event handler.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="e">Property Changed event information.</param>
        void NtpServer_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Valid")
            {
                ConnectCommand.RaiseCanExecuteChanged();
            }
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values["NTPServer"] = _ntpServer.Value;
        }

        /// <summary>
        /// IP changed event handler.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="e">Property Changed event information.</param>
        void Ip_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Valid")
            {
                ConnectCommand.RaiseCanExecuteChanged();
            }
            ReevaluateHasServer();
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values["PeerCCServerIp"] = _ip.Value;
        }


        /// <summary>
        /// Port changed event handler.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="e">Property Changed event information.</param>
        void Port_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Valid")
            {
                ConnectCommand.RaiseCanExecuteChanged();
            }
            ReevaluateHasServer();
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values["PeerCCServerPort"] = _port.Value;
        }

        protected StorageFile WebrtcLoggingFile;


        private Boolean _ntpSyncInProgress;

        /// <summary>
        /// Indicator if sync with ntp is in progress.
        /// </summary>
        public Boolean NtpSyncInProgress
        {
            get { return _ntpSyncInProgress; }
            set { SetProperty(ref _ntpSyncInProgress, value); }
        }

        private bool _ntpSyncEnabled;

        /// <summary>
        /// Indicator and control of NTP syncronization.
        /// </summary>
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
                    NtpSyncInProgress = true;
                    _ntpService.GetNetworkTime(NtpServer.Value);
                }
                else
                {
                    if (NtpSyncInProgress)
                    {
                        NtpSyncInProgress = false;
                        _ntpService.AbortSync();
                    }
                }
            }
        }

        private void HandleNtpTimeSync(long ntpTime)
        {
            Debug.WriteLine("New NTP time: {0}", ntpTime);
#if ORTCLIB
            Org.Ortc.Ortc.NtpServerTime = ntpTime;
#else
            WebRTC.SynNTPTime(ntpTime);
#endif
            NtpSyncInProgress = false;
        }

        private void HandleNtpSynFailed()
        {
            NtpSyncInProgress = false;
            NtpSyncEnabled = false;
        }

        /// <summary>
        /// Application suspending event handler.
        /// </summary>
        public async Task OnAppSuspending()
        {
            Conductor.Instance.CancelConnectingToPeer();

            if (IsConnectedToPeer)
            {
                await Conductor.Instance.DisconnectFromPeer();
            }
            if (IsConnected)
            {
                IsDisconnecting = true;
                await Conductor.Instance.DisconnectFromServer();
            }
            Media.OnAppSuspending();
        }

        /// <summary>
        /// Logic to determine if the peer connection health stats needs to be shown.
        /// </summary>
        public void UpdatePeerConnHealthStatsVisibilityHelper()
        {
            if (IsConnectedToPeer && PeerConnectionHealthStatsEnabled && PeerConnectionHealthStats != null)
            {
                ShowPeerConnectionHealthStats = true;
            }
            else
            {
                ShowPeerConnectionHealthStats = false;
            }
        }

        /// <summary>
        /// Logic to determine if the loopback video UI element needs to be shown.
        /// </summary>
        public void UpdateLoopbackVideoVisibilityHelper()
        {
            if (IsConnectedToPeer && VideoLoopbackEnabled)
            {
                ShowLoopbackVideo = true;
            }
            else
            {
                ShowLoopbackVideo = false;
            }
        }

        // Timer to measure CPU/Memory usage
        private DispatcherTimer _appPerfTimer;

        /// <summary>
        /// Start or stop App Performance check 
        /// </summary>
        private void AppPerformanceCheck()
        {
            if (!_tracingEnabled && !EtwStatsEnabled)
            {
                _appPerfTimer?.Stop();

                return;
            }

            if (_appPerfTimer == null)
            {
                _appPerfTimer = new DispatcherTimer();
                _appPerfTimer.Tick += ReportAppPerfData;
                _appPerfTimer.Interval = new TimeSpan(0, 0, 1); //1 seconds
            }

            _appPerfTimer.Start();
        }

        /// <summary>
        /// Report App performance data.
        /// </summary>
        private void ReportAppPerfData(object sender, object e)
        {
            WebRTC.CpuUsage = CPUData.GetCPUUsage();
            WebRTC.MemoryUsage = MEMData.GetMEMUsage();
        }
    }
}
