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
    internal class Active : BaseCallState
    {
        public override CallState CallState => CallState.ActiveCall;

        public override async Task HangupAsync()
        {
            Context.SaveWebRTCTrace();
            Context.TrackCallEnded();
            var hangingUpState = new HangingUp();
            await Context.SwitchState(hangingUpState);
        }

        public override async Task OnEnteringStateAsync()
        {
            Context.TrackCallStarted();
            ETWEventLogger.Instance.LogEvent("Call Started", DateTimeOffset.Now.ToUnixTimeMilliseconds());
        }

        public override async Task RemoteHangupAsync(RelayMessage message)
        {
            Context.SaveWebRTCTrace();
            Context.TrackCallEnded();
            var hangingUpState = new HangingUp();
            await Context.SwitchState(hangingUpState);
        }

        internal override async Task OnAddStreamAsync(MediaStream stream)
        {
            Context.RemoteStream = stream;
            var tracks = stream.GetVideoTracks();
            if (tracks.Count > 0)
            {
                var source = RtcManager.Instance.Media.CreateMediaSource(tracks[0], CallContext.PeerMediaStreamId);
                Context.RemoteVideoRenderer.SetupRenderer(Context.ForegroundProcessId, source, Context.RemoteVideoControlSize);
            }
        }

        public override async Task OnSdpOfferAsync(RelayMessage message)
        {
            // Re-negotiate the SDP.
            var state = new EstablishIncoming();
            await Context.SwitchState(state);
            // Hand-off the SDP to the new state.
            await state.OnSdpOfferAsync(message);
        }

        public override async Task HoldAsync()
        {
            // Re-negotiate the SDP.
            var state = new EstablishOutgoing(EstablishOutgoing.Reason.HoldCall);
            await Context.SwitchState(state);
        }

        public override async Task CameraSelectionChanged()
        {
            // Re-negotiate the SDP.
            var state = new EstablishOutgoing(EstablishOutgoing.Reason.SwitchCamera);
            await Context.SwitchState(state);
        }
    }
}