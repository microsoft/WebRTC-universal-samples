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
using Windows.Foundation;
using Windows.Storage;
using Windows.System.Threading;
using ChatterBox.Background.AppService;
using ChatterBox.Background.AppService.Dto;
using ChatterBox.Background.Call.Utils;
using ChatterBox.Background.Settings;
using Org.WebRtc;
using DtoMediaDevice = ChatterBox.Background.AppService.Dto.MediaDevice;
using DtoMediaDevices = ChatterBox.Background.AppService.Dto.MediaDevices;
using DtoCodecInfo = ChatterBox.Background.AppService.Dto.CodecInfo;
using DtoCodecInfos = ChatterBox.Background.AppService.Dto.CodecInfos;
using DtoVideoCaptureFormat = ChatterBox.Background.AppService.Dto.VideoCaptureFormat;
using DtoStatsConfig = ChatterBox.Background.AppService.Dto.StatsConfig;
using WebRTCMedia = Org.WebRtc.Media;
using WebRTCCapability = Org.WebRtc.CaptureCapability;

namespace ChatterBox.Background.Call
{
    internal class MediaSettingsChannel : IMediaSettingsChannel
    {
        private ThreadPoolTimer _appPerfTimer;

        private DtoCodecInfo _audioCodec;

        private DtoMediaDevice _audioDevice;

        private DtoMediaDevice _audioPlayoutDevice;

        private DtoStatsConfig _statsConfig;
        private bool _etwStatsEnabled;

        private DtoCodecInfo _videoCodec;

        private DtoMediaDevice _videoDevice;
        private string _webRtcTraceFolderToken;

        public event Action OnVideoDeviceSelectionChanged;

        public bool EtwStatsEnabled
        {
            get { return _etwStatsEnabled; }
            set
            {
                _etwStatsEnabled = value;
                OnTracingStatisticsChanged?.Invoke(_etwStatsEnabled);
            }
        }

        public DtoStatsConfig StatsConfig
        {
            get { return _statsConfig; }
            set { _statsConfig = value; }
        }

        public IAsyncOperation<DtoMediaDevices> GetAudioCaptureDevicesAsync()
        {
            RtcManager.Instance.EnsureRtcIsInitialized();
            var media = RtcManager.Instance.Media;
            var settings = ApplicationData.Current.LocalSettings;
            var audioCaptureDevices = media.GetAudioCaptureDevices().ToArray().ToDto();
            DtoMediaDevice preferredAudioCapture = null;
            // Search for previously selected audio capture device and mark as preferred if found.
            // Otherwise fall back to default device.
            if (settings.Values.ContainsKey(MediaSettingsIds.AudioDeviceSettings))
            {
                var selectedAudioCaptureDevice = Array.Find(
                    audioCaptureDevices.Devices,
                    it => it.Id == (string) settings.Values[MediaSettingsIds.AudioDeviceSettings]);
                if (selectedAudioCaptureDevice != null)
                {
                    // Previously selected audio recording device found, mark as preferred.
                    selectedAudioCaptureDevice.IsPreferred = true;
                    preferredAudioCapture = selectedAudioCaptureDevice;
                }
            }
            if (preferredAudioCapture == null)
            {
                // Previously selected audio recording device is not found anymore,
                // probably removed.
                // Erase user preferrence and select default device.
                _audioDevice = null;
                ApplicationData.Current.LocalSettings.Values.Remove(MediaSettingsIds.AudioDeviceSettings);
            }
            return Task.FromResult(audioCaptureDevices).AsAsyncOperation();
        }

        public IAsyncOperation<DtoCodecInfo> GetAudioCodecAsync()
        {
            return Task.FromResult(_audioCodec).AsAsyncOperation();
        }

        public IAsyncOperation<DtoCodecInfos> GetAudioCodecsAsync()
        {
            RtcManager.Instance.EnsureRtcIsInitialized();
            return Task.FromResult(WebRTC.GetAudioCodecs().ToArray().ToDto()).AsAsyncOperation();
        }

        public IAsyncOperation<DtoMediaDevice> GetAudioDeviceAsync()
        {
            return Task.FromResult(_audioDevice).AsAsyncOperation();
        }

        public IAsyncOperation<DtoMediaDevice> GetAudioPlayoutDeviceAsync()
        {
            return Task.FromResult(_audioPlayoutDevice).AsAsyncOperation();
        }

        public IAsyncOperation<DtoMediaDevices> GetAudioPlayoutDevicesAsync()
        {
            RtcManager.Instance.EnsureRtcIsInitialized();
            var media = RtcManager.Instance.Media;
            var settings = ApplicationData.Current.LocalSettings;
            var audioPlayoutDevices = media.GetAudioPlayoutDevices().ToArray().ToDto();
            DtoMediaDevice preferredAudioPlayoutDevice = null;
            // Search for previously selected audio playout device and mark as preferred if found.
            // Otherwise fall back to default device.
            if (settings.Values.ContainsKey(MediaSettingsIds.AudioPlayoutDeviceSettings))
            {
                var selectedAudioPlayoutDevice = Array.Find(
                    audioPlayoutDevices.Devices,
                    it => it.Id == (string) settings.Values[MediaSettingsIds.AudioPlayoutDeviceSettings]);
                if (selectedAudioPlayoutDevice != null)
                {
                    // Previously selected audio playout device found, mark as preferred.
                    selectedAudioPlayoutDevice.IsPreferred = true;
                    preferredAudioPlayoutDevice = selectedAudioPlayoutDevice;
                }
            }
            if (preferredAudioPlayoutDevice == null)
            {
                // Previously selected audio playout device is not found anymore,
                // probably removed.
                // Erase user preferrence and select default device.

                _audioPlayoutDevice = null;
                ApplicationData.Current.LocalSettings.Values.Remove(MediaSettingsIds.AudioPlayoutDeviceSettings);
            }

            return Task.FromResult(audioPlayoutDevices).AsAsyncOperation();
        }


        public IAsyncOperation<DtoMediaDevices> GetVideoCaptureDevicesAsync()
        {
            RtcManager.Instance.EnsureRtcIsInitialized();
            var media = RtcManager.Instance.Media;
            var settings = ApplicationData.Current.LocalSettings;
            var videoCaptureDevices = media.GetVideoCaptureDevices().ToArray().ToDto();
            DtoMediaDevice preferredVideoCapture = null;
            // Search for previously selected video capture device and mark as preferred if found.
            // Otherwise try to select first available device.
            if (settings.Values.ContainsKey(MediaSettingsIds.VideoDeviceSettings))
            {
                var selectedVideoCaptureDevice = Array.Find(
                    videoCaptureDevices.Devices,
                    it => it.Id == (string) settings.Values[MediaSettingsIds.VideoDeviceSettings]);
                if (selectedVideoCaptureDevice != null)
                {
                    // Previously selected video recording device found, mark as preferred.
                    selectedVideoCaptureDevice.IsPreferred = true;
                    preferredVideoCapture = selectedVideoCaptureDevice;
                    _videoDevice = selectedVideoCaptureDevice;
                }
            }

            if (preferredVideoCapture == null)
            {
                // Previously selected video recording device is not found anymore,
                // probably removed.
                if (videoCaptureDevices.Devices.Length > 0)
                {
                    settings.Values[MediaSettingsIds.VideoDeviceSettings] = videoCaptureDevices.Devices[0].Id;
                    videoCaptureDevices.Devices[0].IsPreferred = true;
                    _videoDevice = videoCaptureDevices.Devices[0];
                    OnVideoDeviceSelectionChanged?.Invoke();
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values.Remove(MediaSettingsIds.VideoDeviceSettings);
                    _videoDevice = null;
                    OnVideoDeviceSelectionChanged?.Invoke();
                }
            }
            return Task.FromResult(videoCaptureDevices).AsAsyncOperation();
        }

        public IAsyncOperation<DtoCodecInfo> GetVideoCodecAsync()
        {
            return Task.FromResult(_videoCodec).AsAsyncOperation();
        }

        public IAsyncOperation<DtoCodecInfos> GetVideoCodecsAsync()
        {
            RtcManager.Instance.EnsureRtcIsInitialized();
            return Task.FromResult(WebRTC.GetVideoCodecs().ToArray().ToDto()).AsAsyncOperation();
        }

        public IAsyncOperation<DtoMediaDevice> GetVideoDeviceAsync()
        {
            var settings = ApplicationData.Current.LocalSettings;
            if (_videoDevice == null && settings.Values.ContainsKey(MediaSettingsIds.VideoDeviceSettings))
            {
                var media = RtcManager.Instance.Media;
                var videoCaptureDevices = media.GetVideoCaptureDevices().ToArray().ToDto();
                _videoDevice = Array.Find(videoCaptureDevices.Devices,
                    it => it.Id == (string) settings.Values[MediaSettingsIds.VideoDeviceSettings]);
            }
            return Task.FromResult(_videoDevice).AsAsyncOperation();
        }

        public IAsyncAction ReleaseDevicesAsync()
        {
            RtcManager.Instance.EnsureRtcIsInitialized();
            WebRTCMedia.OnAppSuspending();
            return Task.CompletedTask.AsAsyncAction();
        }

        public IAsyncAction SaveTraceAsync(TraceServerConfig traceServer)
        {
            RtcManager.Instance.EnsureRtcIsInitialized();
            WebRTC.SaveTrace(traceServer.Ip, traceServer.Port);
            return Task.CompletedTask.AsAsyncAction();
        }

        public IAsyncAction SetAudioCodecAsync(DtoCodecInfo codec)
        {
            _audioCodec = codec;
            ApplicationData.Current.LocalSettings.Values[MediaSettingsIds.AudioCodecSettings] = codec?.Id;
            ETWEventLogger.Instance.LogEvent("Audio Codec Selected",
                "name = " + codec?.Name,
                DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString());
            return Task.CompletedTask.AsAsyncAction();
        }

        public IAsyncAction SetAudioDeviceAsync(DtoMediaDevice device)
        {
            _audioDevice = device;
            var settings = ApplicationData.Current.LocalSettings;
            if (device != null)
            {
                settings.Values[MediaSettingsIds.AudioDeviceSettings] = device.Id;
                ETWEventLogger.Instance.LogEvent("Audio Device Selected",
                    "name = " + device.Name,
                    DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString());
            }
            else
            {
                //Use default device.
                settings.Values.Remove(MediaSettingsIds.AudioDeviceSettings);
            }
            return Task.CompletedTask.AsAsyncAction();
        }

        public IAsyncAction SetAudioPlayoutDeviceAsync(DtoMediaDevice device)
        {
            _audioPlayoutDevice = device;
            var settings = ApplicationData.Current.LocalSettings;
            if (device != null)
            {
                settings.Values[MediaSettingsIds.AudioPlayoutDeviceSettings] = device.Id;
                ETWEventLogger.Instance.LogEvent("Audio Playout Device Selected",
                    "name = " + device.Name,
                    DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString());
            }
            else
            {
                //Use default device.
                settings.Values.Remove(MediaSettingsIds.AudioPlayoutDeviceSettings);
            }
            return Task.CompletedTask.AsAsyncAction();
        }

        public IAsyncAction SetPreferredVideoCaptureFormatAsync(DtoVideoCaptureFormat format)
        {
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values[MediaSettingsIds.PreferredVideoCaptureWidth] = format.Width;
            settings.Values[MediaSettingsIds.PreferredVideoCaptureHeight] = format.Height;
            settings.Values[MediaSettingsIds.PreferredVideoCaptureFrameRate] = format.FrameRate;
            return Task.CompletedTask.AsAsyncAction();
        }

        public IAsyncAction SetTraceFolderTokenAsync(string token)
        {
            _webRtcTraceFolderToken = token;
            return Task.CompletedTask.AsAsyncAction();
        }

        public IAsyncAction SetVideoCodecAsync(DtoCodecInfo codec)
        {
            _videoCodec = codec;
            ApplicationData.Current.LocalSettings.Values[MediaSettingsIds.VideoCodecSettings] = codec?.Id;
            ETWEventLogger.Instance.LogEvent("Video Codec Selected",
                "name = " + codec?.Name,
                DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString());
            return Task.CompletedTask.AsAsyncAction();
        }

        public IAsyncAction SetVideoDeviceAsync(DtoMediaDevice device)
        {
            _videoDevice = device;
            var settings = ApplicationData.Current.LocalSettings;
            if (device != null)
            {
                settings.Values[MediaSettingsIds.VideoDeviceSettings] = device.Id;
                ETWEventLogger.Instance.LogEvent("Video Device Selected",
                    "name = " + device.Name,
                    DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString());

                var selectedVideoDevice = device.FromDto();
                int preferredCaptureWidth = (int)settings.Values[MediaSettingsIds.PreferredVideoCaptureWidth];
                int preferredCaptureHeight = (int)settings.Values[MediaSettingsIds.PreferredVideoCaptureHeight];
                int preferredCaptureFrameRate = (int)settings.Values[MediaSettingsIds.PreferredVideoCaptureFrameRate];
                bool isCapabilityValid = false;
                if (selectedVideoDevice != null)
                {
                    var getTask= selectedVideoDevice.GetVideoCaptureCapabilities().AsTask();
                    getTask.Wait();
                    var capabilities = getTask.Result;
                    foreach (var capability in capabilities)
                    {
                        if (capability.FrameRate == preferredCaptureFrameRate &&
                           capability.Height == preferredCaptureHeight &&
                           capability.Width == preferredCaptureWidth)
                        {
                            isCapabilityValid = true;
                        }
                    }
                }
                if (!isCapabilityValid)
                {
                    preferredCaptureWidth = 640;
                    settings.Values[MediaSettingsIds.PreferredVideoCaptureWidth] = preferredCaptureWidth;
                    preferredCaptureHeight = 480;
                    settings.Values[MediaSettingsIds.PreferredVideoCaptureHeight] = preferredCaptureHeight;
                    preferredCaptureFrameRate = 30;
                    settings.Values[MediaSettingsIds.PreferredVideoCaptureFrameRate] = preferredCaptureFrameRate;
                }
                WebRTC.SetPreferredVideoCaptureFormat(preferredCaptureWidth, preferredCaptureHeight,
                    preferredCaptureFrameRate);
            }
            else
            {
                settings.Values.Remove(MediaSettingsIds.VideoDeviceSettings);
            }
            OnVideoDeviceSelectionChanged?.Invoke();
            return Task.CompletedTask.AsAsyncAction();
        }

        public IAsyncAction StartTraceAsync()
        {
            RtcManager.Instance.EnsureRtcIsInitialized();
            WebRTC.StartTracing();
            AppPerformanceCheck();
            return Task.CompletedTask.AsAsyncAction();
        }

        public IAsyncAction StopTraceAsync()
        {
            RtcManager.Instance.EnsureRtcIsInitialized();
            WebRTC.StopTracing();
            _appPerfTimer?.Cancel();
            return Task.CompletedTask.AsAsyncAction();
        }


        public IAsyncAction SyncWithNtpAsync(long ntpTime)
        {
            RtcManager.Instance.EnsureRtcIsInitialized();
            WebRTC.SynNTPTime(ntpTime);
            return Task.CompletedTask.AsAsyncAction();
        }

        public IAsyncAction ToggleEtwStatsAsync(bool enabled)
        {
            EtwStatsEnabled = enabled;
            ETWEventLogger.Instance.ETWStatsEnabled = enabled;
            return Task.CompletedTask.AsAsyncAction();
        }


        public IAsyncAction SetStatsConfigAsync(StatsConfig config)
        {
            StatsConfig = config;
            return Task.CompletedTask.AsAsyncAction();
        }

        internal string GetTraceFolderToken()
        {
            return _webRtcTraceFolderToken;
        }

        internal event Action<bool> OnTracingStatisticsChanged;

        private void AppPerformanceCheck()
        {
            _appPerfTimer?.Cancel();
            _appPerfTimer = ThreadPoolTimer.CreatePeriodicTimer(t => ReportAppPerf(), TimeSpan.FromSeconds(1));
        }


        private void ReportAppPerf()
        {
            RtcManager.Instance.EnsureRtcIsInitialized();
            WebRTC.CpuUsage = CPUData.GetCPUUsage();
            WebRTC.MemoryUsage = MEMData.GetMEMUsage();
        }
    }
}