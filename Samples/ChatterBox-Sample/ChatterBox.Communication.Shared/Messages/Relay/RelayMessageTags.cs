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

namespace ChatterBox.Communication.Messages.Relay
{
    public static class RelayMessageTags
    {
        public static string Call { get; } = nameof(Call);
        public static string CallAnswer { get; } = nameof(CallAnswer);
        public static string CallHangup { get; } = nameof(CallHangup);
        public static string CallReject { get; } = nameof(CallReject);
        public static string IceCandidate { get; } = nameof(IceCandidate);
        public static string InstantMessage { get; } = nameof(InstantMessage);
        public static string SdpAnswer { get; } = nameof(SdpAnswer);
        public static string SdpOffer { get; } = nameof(SdpOffer);
    }
}