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
using Windows.ApplicationModel.Calls;
using Windows.Foundation.Metadata;
using ChatterBox.Background.AppService.Dto;
using ChatterBox.Background.Avatars;
using ChatterBox.Background.Call.States.Interfaces;
using ChatterBox.Background.Tasks;
using ChatterBox.Communication.Messages.Relay;

namespace ChatterBox.Background.Call
{
    internal class VoipHelper : IVoipHelper
    {
        private VoipPhoneCall _voipCall;

        public bool HasCall()
        {
            return _voipCall != null;
        }

        public void SetCallActive(string userId, bool videoEnabled)
        {
            if (_voipCall != null)
            {
                _voipCall.NotifyCallActive();
            }
            else
            {
                StartOutgoingCall(userId, videoEnabled);
            }
        }

        public void SetCallHeld()
        {
            if (_voipCall == null)
            {
                throw new InvalidOperationException("No active call to hold");
            }

            _voipCall.NotifyCallHeld();
        }

        public async Task StartIncomingCallAsync(RelayMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            // Check if the foreground UI is visible.
            // If it is then we don't trigger an incoming call because the call
            // can be answered from the UI and VoipCallCoordinator doesn't handle
            // this case.
            // As a workaround, when SetCallActive() is called when answered from the UI
            // we create an instance of an outgoing call.
            var foregroundIsVisible = false;
            var state = await Hub.Instance.ForegroundClient.GetForegroundStateAsync();
            if (state != null) foregroundIsVisible = state.IsForegroundVisible;

            if (!foregroundIsVisible)
            {
                var voipCallCoordinator = VoipCallCoordinator.GetDefault();

                _voipCall = voipCallCoordinator.RequestNewIncomingCall(
                    message.FromUserId, message.FromName,
                    message.FromName,
                    AvatarLink.CallCoordinatorUriFor(message.FromAvatar),
                    "ChatterBox",
                    null,
                    "",
                    null,
                    VoipPhoneCallMedia.Audio,
                    new TimeSpan(0, 1, 20));

                SubscribeToVoipCallEvents();
            }
        }

        public void StartOutgoingCall(string userId, bool videoEnabled)
        {
            var capabilities = VoipPhoneCallMedia.Audio;
            if (videoEnabled)
            {
                capabilities |= VoipPhoneCallMedia.Video;
            }
            var voipCallCoordinator = VoipCallCoordinator.GetDefault();
            _voipCall = voipCallCoordinator.RequestNewOutgoingCall(
                userId, userId,
                "ChatterBox",
                capabilities);

            if (_voipCall == null) return;
            SubscribeToVoipCallEvents();
            // Immediately set the call as active.
            _voipCall.NotifyCallActive();
        }

        public async Task StartVoipTask()
        {
            // Make sure there isn't already a voip task active and the contract is available.
            if (Hub.Instance.VoipTaskInstance == null &&
                ApiInformation.IsApiContractPresent("Windows.ApplicationModel.Calls.CallsVoipContract", 1))
            {
                var vcc = VoipCallCoordinator.GetDefault();
                var voipEntryPoint = typeof(VoipTask).FullName;
                try
                {
                    var status = await vcc.ReserveCallResourcesAsync(voipEntryPoint);
                    Debug.WriteLine($"ReserveCallResourcesAsync {voipEntryPoint} result -> {status}");
                }
                catch (Exception ex)
                {
                    const int rtcTaskAlreadyRunningErrorCode = -2147024713;
                    if (ex.HResult == rtcTaskAlreadyRunningErrorCode)
                    {
                        Debug.WriteLine("VoipTask already running");
                    }
                    else
                    {
                        Debug.WriteLine($"ReserveCallResourcesAsync error -> {ex.HResult} : {ex.Message}");
                        throw;
                    }
                }
            }
        }

        public void StopVoip()
        {
            if (_voipCall != null)
            {
                _voipCall.NotifyCallEnded();
                _voipCall = null;
            }

            Hub.Instance.VoipTaskInstance?.CloseVoipTask();
        }

        private void SubscribeToVoipCallEvents()
        {
            if (_voipCall != null)
            {
                _voipCall.AnswerRequested += Call_AnswerRequested;
                _voipCall.EndRequested += Call_EndRequested;
                _voipCall.HoldRequested += Call_HoldRequested;
                _voipCall.RejectRequested += Call_RejectRequested;
                _voipCall.ResumeRequested += Call_ResumeRequested;
            }
        }

        private async void Call_AnswerRequested(VoipPhoneCall sender, CallAnswerEventArgs args)
        {
            _voipCall.NotifyCallActive();
            await Hub.Instance.CallChannel.AnswerAsync();
        }

        private async void Call_EndRequested(VoipPhoneCall sender, CallStateChangeEventArgs args)
        {
            await Hub.Instance.CallChannel.HangupAsync();
        }

        private async void Call_HoldRequested(VoipPhoneCall sender, CallStateChangeEventArgs args)
        {
            await Hub.Instance.CallChannel.HoldAsync();
        }

        private async void Call_RejectRequested(VoipPhoneCall sender, CallRejectEventArgs args)
        {
            await Hub.Instance.CallChannel.RejectAsync(new IncomingCallReject
            {
                Reason = "Rejected"
            });
        }

        private async void Call_ResumeRequested(VoipPhoneCall sender, CallStateChangeEventArgs args)
        {
            await Hub.Instance.CallChannel.ResumeAsync();
        }
    }
}