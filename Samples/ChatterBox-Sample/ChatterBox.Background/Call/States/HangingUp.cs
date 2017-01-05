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
using System.Collections.Generic;
using System.Threading.Tasks;
using ChatterBox.Background.AppService.Dto;
using ChatterBox.Communication.Messages.Relay;
using Org.WebRtc;

namespace ChatterBox.Background.Call.States
{
    internal class HangingUp : BaseCallState
    {
        public override CallState CallState => CallState.HangingUp;

        public override async Task OnEnteringStateAsync()
        {
            Context.SendToPeer(RelayMessageTags.CallHangup, "");
            if (Context.PeerConnection != null)
            {
                Context.PeerConnection.Close();
                Context.PeerConnection = null;
                Context.PeerId = null;
            }

            StopTracks(Context.LocalStream?.GetTracks());
            Context.LocalStream?.Stop();
            Context.LocalStream = null;

            StopTracks(Context.RemoteStream?.GetTracks());
            Context.RemoteStream?.Stop();
            Context.RemoteStream = null;

            Context.ResetRenderers();

            var idleState = new Idle();
            ETWEventLogger.Instance.LogEvent("Call Hangup", DateTimeOffset.Now.ToUnixTimeMilliseconds());

            await Context.SwitchState(idleState);
        }

        private void StopTracks(IList<IMediaStreamTrack> tracks)
        {
            if (tracks == null) return;
            foreach (var track in tracks)
            {
                track.Stop();
            }
        }
    }
}