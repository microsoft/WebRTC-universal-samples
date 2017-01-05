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

using System.Threading.Tasks;
using ChatterBox.Background.AppService.Dto;
using ChatterBox.Communication.Messages.Relay;

#pragma warning disable 1998

namespace ChatterBox.Background.Call.States
{
    internal class Idle : BaseCallState
    {
        public override CallState CallState => CallState.Idle;

        public override async Task CallAsync(OutgoingCallRequest request)
        {
            var remoteRingingState = new RemoteRinging(request);
            await Context.SwitchState(remoteRingingState);
        }

        public override async Task IncomingCallAsync(RelayMessage message)
        {
            var localRingingState = new LocalRinging(message);
            await Context.SwitchState(localRingingState);
        }

        public override async Task OnEnteringStateAsync()
        {
            // Entering idle state.
            Context.VoipHelper.StopVoip();

            // Make sure the context is sane.
            Context.PeerConnection = null;
            Context.PeerId = null;

            Context.CallType = CallType.NotInCall;
            Context.VideoCodecUsed = null;
        }

        public override async Task OnLeavingStateAsync()
        {
            // Leaving the idle state means there's a call that's happening.
            // Trigger the VoipTask to prevent this background task from terminating.
            await Context.VoipHelper.StartVoipTask();
            Context.InitializeRTC();
        }
    }
}