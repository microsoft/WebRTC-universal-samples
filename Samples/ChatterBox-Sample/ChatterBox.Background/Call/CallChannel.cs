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
using Windows.Foundation;
using Windows.Graphics.Display;
using ChatterBox.Background.AppService;
using ChatterBox.Background.AppService.Dto;
using ChatterBox.Background.Settings;
using ChatterBox.Communication.Messages.Relay;

namespace ChatterBox.Background.Call
{
    internal class CallChannel :
        ICallChannel
    {
        private readonly IHub _hub;

        public CallChannel(IHub hub, CallContext context)
        {
            _hub = hub;
            Context = context;
        }

        private CallContext Context { get; }


        public IAsyncAction AnswerAsync()
        {
            return Context.WithState(st => st.AnswerAsync()).AsAsyncAction();
        }

        public IAsyncAction CallAsync(OutgoingCallRequest request)
        {
            return Context.WithState(st => st.CallAsync(request)).AsAsyncAction();
        }

        public IAsyncAction ConfigureMicrophoneAsync(MicrophoneConfig config)
        {
            return Context.WithContextAction(cx => { cx.MicrophoneMuted = config.Muted; }).AsAsyncAction();
        }

        public IAsyncAction ConfigureVideoAsync(VideoConfig config)
        {
            return Context.WithContextAction(cx => { cx.IsVideoEnabled = config.On; }).AsAsyncAction();
        }

        public IAsyncAction DisplayOrientationChangedAsync(DisplayOrientations orientation)
        {
            RtcManager.Instance.DisplayOrientation = orientation;
            return Task.CompletedTask.AsAsyncAction();
        }

        public IAsyncOperation<CallStatus> GetCallStatusAsync()
        {
            return Task.FromResult(Context.GetCallStatus()).AsAsyncOperation();
        }

        public IAsyncOperation<FrameFormat> GetFrameFormatAsync(bool local)
        {
            return Task.FromResult(Context.GetFrameFormat(local)).AsAsyncOperation();
        }

        // Hangup can happen on both sides
        public IAsyncAction HangupAsync()
        {
            return Context.WithState(st => st.HangupAsync()).AsAsyncAction();
        }

        public IAsyncAction InitializeRtcAsync()
        {
            return Context.WithContextAction(cx => { cx.InitializeRTC(); }).AsAsyncAction();
        }

        public IAsyncAction OnIceCandidateAsync(RelayMessage message)
        {
            return Context.WithState(st => st.AddRemoteIceCandidateAsync(message)).AsAsyncAction();
        }

        // Remotely initiated calls
        public IAsyncAction OnIncomingCallAsync(RelayMessage message)
        {
            return Context.WithState(st => st.IncomingCallAsync(message)).AsAsyncAction();
        }

        public IAsyncAction OnLocalControlSizeAsync(VideoControlSize size)
        {
            return Context.WithContextAction(cx =>
            {
                cx.LocalVideoControlSize = size.Size;
                cx.LocalVideoRenderer.SetRenderControlSize(size.Size);
            }).AsAsyncAction();
        }

        public IAsyncAction OnOutgoingCallAcceptedAsync(RelayMessage message)
        {
            return Context.WithState(st => st.OutgoingCallAcceptedAsync(message)).AsAsyncAction();
        }

        public IAsyncAction OnOutgoingCallRejectedAsync(RelayMessage message)
        {
            return Context.WithState(st => st.OutgoingCallRejectedAsync(message)).AsAsyncAction();
        }

        public IAsyncAction OnRemoteControlSizeAsync(VideoControlSize size)
        {
            return Context.WithContextAction(cx =>
            {
                cx.RemoteVideoControlSize = size.Size;
                cx.RemoteVideoRenderer.SetRenderControlSize(size.Size);
            }).AsAsyncAction();
        }

        public IAsyncAction OnRemoteHangupAsync(RelayMessage message)
        {
            if (Context.PeerConnection != null)
            {
                if (!message.FromUserId.Equals(Context.PeerId))
                {
                    // Don't hang up if a call is in progress and a third
                    // user is calling then is hanging up
                    // (intentionally or by call timeout).
                    return Task.CompletedTask.AsAsyncAction();
                }
            }
            return Context.WithState(st => st.RemoteHangupAsync(message)).AsAsyncAction();
        }

        // WebRTC signalling
        public IAsyncAction OnSdpAnswerAsync(RelayMessage message)
        {
            ETWEventLogger.Instance.LogEvent("Sdp Answer", "Payload is: " + message.Payload,
                    DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString());
            return Context.WithState(st => st.OnSdpAnswerAsync(message)).AsAsyncAction();
        }

        public IAsyncAction OnSdpOfferAsync(RelayMessage message)
        {
            ETWEventLogger.Instance.LogEvent("Sdp Offer", "Payload is: " + message.Payload,
                    DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString());
            return Context.WithState(st => st.OnSdpOfferAsync(message)).AsAsyncAction();
        }

        public IAsyncAction RejectAsync(IncomingCallReject reason)
        {
            return Context.WithState(st => st.RejectAsync(reason)).AsAsyncAction();
        }

        public IAsyncAction ResumeCallVideoAsync()
        {
            return Context.WithState(st => st.ResumeCallVideoAsync()).AsAsyncAction();
        }

        public IAsyncAction SetForegroundProcessIdAsync(uint processId)
        {
            return Context.WithContextAction(cx =>
            {
                Context.ForegroundProcessId = processId;
                Context.LocalVideoRenderer?.UpdateForegroundProcessId(processId);
                Context.RemoteVideoRenderer?.UpdateForegroundProcessId(processId);
            }).AsAsyncAction();
        }

        public IAsyncAction SuspendCallVideoAsync()
        {
            return Context.WithState(st => st.SuspendCallVideoAsync()).AsAsyncAction();
        }

        public IAsyncAction HoldAsync()
        {
            return Context.WithState(st => st.HoldAsync()).AsAsyncAction();
        }

        public IAsyncAction ResumeAsync()
        {
            return Context.WithState(st => st.ResumeAsync()).AsAsyncAction();
        }
    }
}