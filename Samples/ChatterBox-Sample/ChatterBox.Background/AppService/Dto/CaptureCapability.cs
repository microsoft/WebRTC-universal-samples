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

using System.Runtime.Serialization;

namespace ChatterBox.Background.AppService.Dto
{
    public sealed class CaptureCapability
    {
        [DataMember]
        public uint FrameRate { get; set; }

        [DataMember]
        public string FrameRateDescription { get; set; }

        [DataMember]
        public string FullDescription { get; set; }

        [DataMember]
        public uint Height { get; set; }

        [DataMember]
        public MediaRatio PixelAspectRatio { get; set; }

        [DataMember]
        public string ResolutionDescription { get; set; }

        [DataMember]
        public uint Width { get; set; }
    }
}