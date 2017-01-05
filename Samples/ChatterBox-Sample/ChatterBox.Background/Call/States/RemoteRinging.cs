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
using System.Threading;
using System.Threading.Tasks;
using ChatterBox.Background.AppService.Dto;
using ChatterBox.Communication.Messages.Relay;
using Newtonsoft.Json;

#pragma warning disable 1998

namespace ChatterBox.Background.Call.States
{
    internal class RemoteRinging : BaseCallState
    {
        private readonly OutgoingCallRequest _request;
        private Timer _callTimeout;

        public RemoteRinging(OutgoingCallRequest request)
        {
            _request = request;
        }

        public override CallState CallState => CallState.RemoteRinging;

        public override async Task HangupAsync()
        {
            var hangingUpState = new HangingUp();
            ETWEventLogger.Instance.LogEvent("Reject Call", DateTimeOffset.Now.ToUnixTimeMilliseconds());
            await Context.SwitchState(hangingUpState);
        }

        public override async Task OnEnteringStateAsync()
        {
            Debug.Assert(Context.PeerConnection == null);

            Context.PeerId = _request.PeerUserId;
            Context.IsVideoEnabled = _request.VideoEnabled;

            var payload = JsonConvert.SerializeObject(_request);

            Context.SendToPeer(RelayMessageTags.Call, payload);

            ETWEventLogger.Instance.LogEvent("Remote Ringing", DateTimeOffset.Now.ToUnixTimeMilliseconds());

            _callTimeout = new Timer(CallTimeoutCallback, null, 30000, Timeout.Infinite);

            Context.CallType = _request.VideoEnabled ? CallType.AudioVideo : CallType.Audio;
        }

        public override Task OnLeavingStateAsync()
        {
            StopTimer();
            return base.OnLeavingStateAsync();
        }

        public override async Task OutgoingCallAcceptedAsync(RelayMessage message)
        {
            // We didn't have the PeerName when initiating the outgoing call
            // but that field is populated on the remote answered message.
            Context.PeerName = message.FromName;
            var establishOutgoingState = new EstablishOutgoing(EstablishOutgoing.Reason.EstablishCall);
            await Context.SwitchState(establishOutgoingState);
        }

        public override async Task OutgoingCallRejectedAsync(RelayMessage message)
        {
            var hangingUpState = new HangingUp();
            await Context.SwitchState(hangingUpState);
        }

        public override async Task RemoteHangupAsync(RelayMessage message)
        {
            var hangingUpState = new HangingUp();
            await Context.SwitchState(hangingUpState);
        }

        private async void CallTimeoutCallback(object state)
        {
            if (Context != null)
            {
                await HangupAsync();
            }
            else
            {
                StopTimer();
            }
        }

        private void StopTimer()
        {
            if (_callTimeout != null)
            {
                _callTimeout.Dispose();
                _callTimeout = null;
            }
        }
    }
}