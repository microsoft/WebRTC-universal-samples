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

namespace ChatterBox.Background.AppService.Dto
{
    public sealed class CallStatus
    {
        public bool HasPeerConnection { get; set; }
        public bool IsVideoEnabled { get; set; }
        public string PeerId { get; set; }
        public CallState State { get; set; }
        public CallType Type { get; set; }
    }
}