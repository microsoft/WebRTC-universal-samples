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

using Windows.Foundation;
using ChatterBox.Background.AppService.Dto;

namespace ChatterBox.Background.AppService
{
    public interface IMediaSettingsChannel
    {
        IAsyncOperation<MediaDevices> GetAudioCaptureDevicesAsync();

        IAsyncOperation<CodecInfo> GetAudioCodecAsync();
        IAsyncOperation<CodecInfos> GetAudioCodecsAsync();
        IAsyncOperation<MediaDevice> GetAudioDeviceAsync();
        IAsyncOperation<MediaDevice> GetAudioPlayoutDeviceAsync();
        IAsyncOperation<MediaDevices> GetAudioPlayoutDevicesAsync();
        IAsyncOperation<MediaDevices> GetVideoCaptureDevicesAsync();
        IAsyncOperation<CodecInfo> GetVideoCodecAsync();
        IAsyncOperation<CodecInfos> GetVideoCodecsAsync();
        IAsyncOperation<MediaDevice> GetVideoDeviceAsync();

        IAsyncAction ReleaseDevicesAsync();
        IAsyncAction SaveTraceAsync(TraceServerConfig config);
        IAsyncAction SetAudioCodecAsync(CodecInfo codec);
        IAsyncAction SetAudioDeviceAsync(MediaDevice device);
        IAsyncAction SetAudioPlayoutDeviceAsync(MediaDevice device);
        IAsyncAction SetPreferredVideoCaptureFormatAsync(VideoCaptureFormat format);
        IAsyncAction SetTraceFolderTokenAsync(string token);
        IAsyncAction SetVideoCodecAsync(CodecInfo codec);
        IAsyncAction SetVideoDeviceAsync(MediaDevice device);
        IAsyncAction StartTraceAsync();
        IAsyncAction StopTraceAsync();
        IAsyncAction SyncWithNtpAsync(long ntpTime);
        IAsyncAction ToggleEtwStatsAsync(bool enabled);
        IAsyncAction SetStatsConfigAsync(StatsConfig config);
    }
}