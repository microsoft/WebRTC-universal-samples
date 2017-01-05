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
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics.Display;
using Windows.Storage;
using ChatterBox.Background.AppService.Dto;
using ChatterBox.Background.Settings;
using Org.WebRtc;
using WebRTCMedia = Org.WebRtc.Media;

namespace ChatterBox.Background.Call
{
    internal class RtcManager
    {
        private static RtcManager _instance;
        private static readonly object SingletonLock = new object();

        private readonly object _lock = new object();

        private DisplayOrientations _displayOrientation = DisplayOrientations.None;

        private bool _rtcIsInitialized;

        private RtcManager()
        {
        }

        public DisplayOrientations DisplayOrientation
        {
            get
            {
                lock (_lock)
                {
                    return _displayOrientation;
                }
            }
            set
            {
                lock (_lock)
                {
                    _displayOrientation = value;
                    if (_rtcIsInitialized)
                    {
                        WebRTCMedia.SetDisplayOrientation(_displayOrientation);
                    }
                }
            }
        }

        public static RtcManager Instance
        {
            get
            {
                lock (SingletonLock)
                {
                    if (_instance == null)
                        _instance = new RtcManager();
                }
                return _instance;
            }
        }

        /// <summary>
        ///     We keep only one instance of Media.
        /// </summary>
        public WebRTCMedia Media { get; private set; }

        public async Task ConfigureRtcAsync()
        {
            var settings = ApplicationData.Current.LocalSettings;
            var videoDeviceId = string.Empty;
            if (settings.Values.ContainsKey(MediaSettingsIds.VideoDeviceSettings))
            {
                videoDeviceId = (string) settings.Values[MediaSettingsIds.VideoDeviceSettings];
            }
            var videoDevices = Media.GetVideoCaptureDevices();
            var selectedVideoDevice = videoDevices.FirstOrDefault(d => d.Id.Equals(videoDeviceId));
            selectedVideoDevice = selectedVideoDevice ?? videoDevices.FirstOrDefault();
            if (selectedVideoDevice != null)
            {
                Media.SelectVideoDevice(selectedVideoDevice);
            }

            if (settings.Values.ContainsKey(MediaSettingsIds.AudioDeviceSettings))
            {
                var audioDeviceId = (string) settings.Values[MediaSettingsIds.AudioDeviceSettings];
                var audioDevices = Media.GetAudioCaptureDevices();
                var selectedAudioDevice = audioDevices.FirstOrDefault(d => d.Id.Equals(audioDeviceId));
                if (selectedAudioDevice == null)
                {
                    settings.Values.Remove(MediaSettingsIds.AudioDeviceSettings);
                }
                Media.SelectAudioCaptureDevice(selectedAudioDevice);
            }
            else
            {
                Media.SelectAudioCaptureDevice(null);
            }

            if (settings.Values.ContainsKey(MediaSettingsIds.AudioPlayoutDeviceSettings))
            {
                var audioPlayoutDeviceId = (string) settings.Values[MediaSettingsIds.AudioPlayoutDeviceSettings];
                var audioPlayoutDevices = Media.GetAudioPlayoutDevices();
                var selectedAudioPlayoutDevice =
                    audioPlayoutDevices.FirstOrDefault(d => d.Id.Equals(audioPlayoutDeviceId));
                if (selectedAudioPlayoutDevice == null)
                {
                    settings.Values.Remove(MediaSettingsIds.AudioPlayoutDeviceSettings);
                }
                Media.SelectAudioPlayoutDevice(selectedAudioPlayoutDevice);
            }
            else
            {
                Media.SelectAudioPlayoutDevice(null);
            }

            var videoCodecId = int.MinValue;
            if (settings.Values.ContainsKey(MediaSettingsIds.VideoCodecSettings))
            {
                videoCodecId = (int) settings.Values[MediaSettingsIds.VideoCodecSettings];
            }
            var videoCodecs = WebRTC.GetVideoCodecs();
            var selectedVideoCodec = videoCodecs.FirstOrDefault(c => c.Id.Equals(videoCodecId));
            await
                Hub.Instance.MediaSettingsChannel.SetVideoCodecAsync(
                    (selectedVideoCodec ?? videoCodecs.FirstOrDefault()).ToDto());

            var audioCodecId = int.MinValue;
            if (settings.Values.ContainsKey(MediaSettingsIds.AudioCodecSettings))
            {
                audioCodecId = (int) settings.Values[MediaSettingsIds.AudioCodecSettings];
            }
            var audioCodecs = WebRTC.GetAudioCodecs();
            var selectedAudioCodec = audioCodecs.FirstOrDefault(c => c.Id.Equals(audioCodecId));
            await
                Hub.Instance.MediaSettingsChannel.SetAudioCodecAsync(
                    (selectedAudioCodec ?? audioCodecs.FirstOrDefault()).ToDto());

            if (settings.Values.ContainsKey(MediaSettingsIds.PreferredVideoCaptureWidth) &&
                settings.Values.ContainsKey(MediaSettingsIds.PreferredVideoCaptureHeight) &&
                settings.Values.ContainsKey(MediaSettingsIds.PreferredVideoCaptureFrameRate))
            {
                WebRTC.SetPreferredVideoCaptureFormat(
                    (int) settings.Values[MediaSettingsIds.PreferredVideoCaptureWidth],
                    (int) settings.Values[MediaSettingsIds.PreferredVideoCaptureHeight],
                    (int) settings.Values[MediaSettingsIds.PreferredVideoCaptureFrameRate]);
            }
        }

        public void EnsureRtcIsInitialized()
        {
            lock (_lock)
            {
                if (!_rtcIsInitialized)
                {
                    // On Windows 10, we don't need to use the CoreDispatcher.
                    // Pass null to initialize.
                    WebRTC.Initialize(null);
                    // Cache the media object for later use.
                    Media = WebRTCMedia.CreateMedia();
                    _rtcIsInitialized = true;
                    Media.OnMediaDevicesChanged += OnMediaDevicesChanged;
                    WebRTCMedia.SetDisplayOrientation(_displayOrientation);

                    // Uncomment the following line to enable WebRTC logging.
                    // Logs are:
                    //  - Saved to local storage. Log folder location can be obtained using WebRTC.LogFolder()
                    //  - Sent over network if client is connected to TCP port 47003
                    //WebRTC.EnableLogging(LogLevel.LOGLVL_INFO);
                }
            }
        }

        private static async void OnMediaDevicesChanged(MediaDeviceType mediaType)
        {
            var changeType = MediaDeviceChangeType.Unknown;
            switch (mediaType)
            {
                case MediaDeviceType.MediaDeviceType_AudioCapture:
                    changeType = MediaDeviceChangeType.AudioCapture;
                    break;
                case MediaDeviceType.MediaDeviceType_AudioPlayout:
                    changeType = MediaDeviceChangeType.AudioPlayout;
                    break;
                case MediaDeviceType.MediaDeviceType_VideoCapture:
                    changeType = MediaDeviceChangeType.VideoCapture;
                    break;
            }
            await Hub.Instance.OnChangeMediaDevices(new MediaDevicesChange
            {
                Type = changeType
            });
        }
    }
}