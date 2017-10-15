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
using System.Diagnostics;
using System.Threading.Tasks;
using ChatterBox.Background.AppService.Dto;
using ChatterBox.Background.Signalling.Dto;
using ChatterBox.Communication.Messages.Relay;
using Newtonsoft.Json;
using Org.WebRtc;

#pragma warning disable 1998

namespace ChatterBox.Background.Call.States
{
    internal class Held : BaseCallState
    {
        public override CallState CallState => CallState.Held;

        public override async Task HangupAsync()
        {
            Context.SaveWebRTCTrace();
            Context.TrackCallEnded();
            var hangingUpState = new HangingUp();
            await Context.SwitchState(hangingUpState);
        }

        public override async Task OnEnteringStateAsync()
        {
            ETWEventLogger.Instance.LogEvent("Call Held", DateTimeOffset.Now.ToUnixTimeMilliseconds());
        }

        public override async Task RemoteHangupAsync(RelayMessage message)
        {
            Context.SaveWebRTCTrace();
            Context.TrackCallEnded();
            var hangingUpState = new HangingUp();
            await Context.SwitchState(hangingUpState);
        }

        public override async Task OnSdpOfferAsync(RelayMessage message)
        {
            // Re-negotiate the SDP.
            var state = new EstablishIncoming();
            await Context.SwitchState(state);
            // Hand-off the SDP to the new state.
            await state.OnSdpOfferAsync(message);
        }

        public override async Task ResumeAsync()
        {
            // Re-negotiate the SDP.
            var state = new EstablishOutgoing(EstablishOutgoing.Reason.EstablishCall);
            await Context.SwitchState(state);
        }

    }
}