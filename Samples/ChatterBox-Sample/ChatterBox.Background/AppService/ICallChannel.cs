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

using Windows.Foundation;
using Windows.Graphics.Display;
using ChatterBox.Background.AppService.Dto;
using ChatterBox.Communication.Messages.Relay;

namespace ChatterBox.Background.AppService
{
    public interface ICallChannel
    {
        IAsyncAction AnswerAsync();
        // Locally initiated calls
        IAsyncAction CallAsync(OutgoingCallRequest request);
        IAsyncAction ConfigureMicrophoneAsync(MicrophoneConfig config);
        IAsyncAction ConfigureVideoAsync(VideoConfig config);
        IAsyncAction DisplayOrientationChangedAsync(DisplayOrientations orientation);
        IAsyncOperation<CallStatus> GetCallStatusAsync();
        IAsyncOperation<FrameFormat> GetFrameFormatAsync(bool local);
        // Hangup can happen on both sides
        IAsyncAction HangupAsync();
        IAsyncAction InitializeRtcAsync();
        IAsyncAction OnIceCandidateAsync(RelayMessage message);
        // Remotely initiated calls
        IAsyncAction OnIncomingCallAsync(RelayMessage message);
        IAsyncAction OnLocalControlSizeAsync(VideoControlSize size);
        IAsyncAction OnOutgoingCallAcceptedAsync(RelayMessage message);
        IAsyncAction OnOutgoingCallRejectedAsync(RelayMessage message);
        IAsyncAction OnRemoteControlSizeAsync(VideoControlSize size);
        IAsyncAction OnRemoteHangupAsync(RelayMessage message);
        // WebRTC signaling
        IAsyncAction OnSdpAnswerAsync(RelayMessage message);
        IAsyncAction OnSdpOfferAsync(RelayMessage message);
        IAsyncAction RejectAsync(IncomingCallReject reason);
        IAsyncAction ResumeCallVideoAsync();
        IAsyncAction SetForegroundProcessIdAsync(uint processId);

        IAsyncAction SuspendCallVideoAsync();
        IAsyncAction HoldAsync();
        IAsyncAction ResumeAsync();
    }
}