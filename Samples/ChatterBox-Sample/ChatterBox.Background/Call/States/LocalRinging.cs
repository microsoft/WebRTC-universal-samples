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
    internal class LocalRinging : BaseCallState
    {
        private const int CallDueTimeout = 1000*35; //35 seconds, should be bigger than RemoteRinging state timer
        private readonly OutgoingCallRequest _callRequest;
        private readonly RelayMessage _message;
        private Timer _callTimeout;

        public LocalRinging(RelayMessage message)
        {
            _message = message;
            try
            {
                _callRequest =
                    (OutgoingCallRequest) JsonConvert.DeserializeObject(message.Payload, typeof (OutgoingCallRequest));
            }
            catch (Exception)
            {
                if (Debugger.IsAttached)
                {
                    throw;
                }
            }
        }

        public override CallState CallState => CallState.LocalRinging;

        public override async Task AnswerAsync()
        {
            Context.SendToPeer(RelayMessageTags.CallAnswer, "");

            var establishIncomingState = new EstablishIncoming();
            await Context.SwitchState(establishIncomingState);
            ETWEventLogger.Instance.LogEvent("Call Answered", DateTimeOffset.Now.ToUnixTimeMilliseconds());
        }

        public override async Task HangupAsync()
        {
            var hangingUpState = new HangingUp();
            await Context.SwitchState(hangingUpState);
        }

        public override async Task OnEnteringStateAsync()
        {
            Debug.Assert(Context.PeerConnection == null);
            Context.PeerId = _message.FromUserId;
            Context.PeerName = _message.FromName;
            Context.IsVideoEnabled = _callRequest.VideoEnabled;

            await Context.VoipHelper.StartIncomingCallAsync(_message);
            ETWEventLogger.Instance.LogEvent("Local Ringing", DateTimeOffset.Now.ToUnixTimeMilliseconds());

            _callTimeout = new Timer(CallTimeoutCallback, null, CallDueTimeout, Timeout.Infinite);

            Context.CallType = _callRequest.VideoEnabled ? CallType.AudioVideo : CallType.Audio;
        }

        public override Task OnLeavingStateAsync()
        {
            StopTimer();
            return base.OnLeavingStateAsync();
        }

        public override async Task RejectAsync(IncomingCallReject reason)
        {
            Context.SendToPeer(RelayMessageTags.CallReject, "Rejected");
            var hangingUpState = new HangingUp();
            ETWEventLogger.Instance.LogEvent("Reject Call", DateTimeOffset.Now.ToUnixTimeMilliseconds());
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
            if (_callTimeout == null) return;
            _callTimeout.Dispose();
            _callTimeout = null;
        }
    }
}