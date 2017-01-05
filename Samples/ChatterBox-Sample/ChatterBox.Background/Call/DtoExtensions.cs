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

using System.Linq;
using Windows.Media.MediaProperties;
using ChatterBox.Background.Signaling.Dto;
using Org.WebRtc;
using DtoMediaDevice = ChatterBox.Background.AppService.Dto.MediaDevice;
using DtoMediaDeviceLocation = ChatterBox.Background.AppService.Dto.MediaDeviceLocation;
using DtoMediaDevices = ChatterBox.Background.AppService.Dto.MediaDevices;
using DtoCodecInfo = ChatterBox.Background.AppService.Dto.CodecInfo;
using DtoCodecInfos = ChatterBox.Background.AppService.Dto.CodecInfos;
using DtoMediaRatio = ChatterBox.Background.AppService.Dto.MediaRatio;
using DtoCaptureCapability = ChatterBox.Background.AppService.Dto.CaptureCapability;
using DtoCaptureCapabilities = ChatterBox.Background.AppService.Dto.CaptureCapabilities;

namespace ChatterBox.Background.Call
{
    internal static class DtoExtensions
    {
        public static RTCIceCandidate FromDto(this DtoIceCandidate obj)
        {
            if (obj == null)
                return null;
            return new RTCIceCandidate
            {
                Candidate = obj.Candidate,
                SdpMid = obj.SdpMid,
                SdpMLineIndex = obj.SdpMLineIndex
            };
        }

        public static RTCIceCandidate[] FromDto(this DtoIceCandidates obj)
        {
            if (obj == null || obj.Candidates == null)
                return null;
            return obj.Candidates.Select(FromDto).ToArray();
        }

        public static MediaDevice FromDto(this DtoMediaDevice obj)
        {
            if (obj == null)
                return null;
            return new MediaDevice(obj.Id, obj.Name);
        }

        public static MediaDevice[] FromDto(this DtoMediaDevices obj)
        {
            if (obj == null || obj.Devices == null)
                return null;
            return obj.Devices.Select(FromDto).ToArray();
        }

        public static CodecInfo FromDto(this DtoCodecInfo obj)
        {
            if (obj == null)
                return null;
            return new CodecInfo(obj.Id, obj.ClockRate, obj.Name);
        }

        public static CodecInfo[] FromDto(this DtoCodecInfos obj)
        {
            if (obj == null || obj.Codecs == null)
                return null;
            return obj.Codecs.Select(FromDto).ToArray();
        }

        public static DtoIceCandidate ToDto(this RTCIceCandidate obj)
        {
            if (obj == null)
                return null;
            return new DtoIceCandidate
            {
                Candidate = obj.Candidate,
                SdpMid = obj.SdpMid,
                SdpMLineIndex = obj.SdpMLineIndex
            };
        }

        public static DtoIceCandidates ToDto(this RTCIceCandidate[] obj)
        {
            if (obj == null)
                return null;
            return new DtoIceCandidates
            {
                Candidates = obj.Select(ToDto).ToArray()
            };
        }

        public static DtoMediaDevice ToDto(this MediaDevice obj)
        {
            if (obj == null)
                return null;
            DtoMediaDevice result = new DtoMediaDevice
            {
                Id = obj.Id,
                Name = obj.Name,
                IsPreferred = false
            };
            result.Location = DtoMediaDeviceLocation.Unkown;
            if (obj.Location != null)
            {
                if(obj.Location.Panel == Windows.Devices.Enumeration.Panel.Front)
                {
                    result.Location = DtoMediaDeviceLocation.Front;
                }
                else if (obj.Location.Panel == Windows.Devices.Enumeration.Panel.Back)
                {
                    result.Location = DtoMediaDeviceLocation.Back;
                }
            }
            return result;
        }

        public static DtoMediaDevices ToDto(this MediaDevice[] obj)
        {
            if (obj == null)
                return null;
            return new DtoMediaDevices
            {
                Devices = obj.Select(ToDto).ToArray()
            };
        }

        public static DtoCodecInfo ToDto(this CodecInfo obj)
        {
            if (obj == null)
                return null;
            return new DtoCodecInfo
            {
                Id = obj.Id,
                Name = obj.Name,
                ClockRate = obj.ClockRate
            };
        }

        public static DtoCodecInfos ToDto(this CodecInfo[] obj)
        {
            if (obj == null)
                return null;
            return new DtoCodecInfos
            {
                Codecs = obj.Select(ToDto).ToArray()
            };
        }

        public static DtoMediaRatio ToDto(this MediaRatio obj)
        {
            if (obj == null)
                return null;
            return new DtoMediaRatio
            {
                Numerator = obj.Numerator,
                Denominator = obj.Denominator
            };
        }

        public static DtoCaptureCapability ToDto(this CaptureCapability obj)
        {
            if (obj == null)
                return null;
            return new DtoCaptureCapability
            {
                FrameRate = obj.FrameRate,
                FrameRateDescription = obj.FrameRateDescription,
                FullDescription = obj.FullDescription,
                Height = obj.Height,
                PixelAspectRatio = ToDto(obj.PixelAspectRatio),
                ResolutionDescription = obj.ResolutionDescription,
                Width = obj.Width
            };
        }

        public static DtoCaptureCapabilities ToDto(this CaptureCapability[] obj)
        {
            return new DtoCaptureCapabilities
            {
                Capabilities = obj.Select(ToDto).ToArray()
            };
        }
    }
}