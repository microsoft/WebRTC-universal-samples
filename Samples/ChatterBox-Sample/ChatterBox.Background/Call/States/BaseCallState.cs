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
using System.Threading.Tasks;
using ChatterBox.Background.AppService.Dto;
using ChatterBox.Communication.Messages.Relay;
using Org.WebRtc;
using ChatterBox.Background.Signalling.Dto;
using Newtonsoft.Json;
using System.Diagnostics;

#pragma warning disable 1998

namespace ChatterBox.Background.Call.States
{
    internal abstract class BaseCallState
    {
        public abstract CallState CallState { get; }
        public CallContext Context { get; private set; }

        public virtual async Task AnswerAsync()
        {
        }

        public virtual async Task CallAsync(OutgoingCallRequest request)
        {
        }

        public async Task EnterStateAsync(CallContext context)
        {
            Context = context;
            await OnEnteringStateAsync();
        }

        public virtual async Task HangupAsync()
        {
        }

        public virtual async Task IncomingCallAsync(RelayMessage message)
        {
        }

        public async Task LeaveStateAsync()
        {
            await OnLeavingStateAsync();
            Context = null;
        }

        public virtual async Task OnEnteringStateAsync()
        {
        }

        public virtual async Task OnLeavingStateAsync()
        {
        }

        public virtual async Task OnSdpAnswerAsync(RelayMessage message)
        {
        }

        public virtual async Task OnSdpOfferAsync(RelayMessage message)
        {
        }

        public virtual async Task OutgoingCallAcceptedAsync(RelayMessage message)
        {
        }

        public virtual async Task OutgoingCallRejectedAsync(RelayMessage message)
        {
        }

        public virtual async Task RejectAsync(IncomingCallReject reason)
        {
        }

        public virtual async Task RemoteHangupAsync(RelayMessage message)
        {
        }

        internal virtual async Task OnAddStreamAsync(MediaStream stream)
        {
        }

        public virtual async Task HoldAsync()
        {
        }

        public virtual async Task ResumeAsync()
        {
        }

        /// <summary>
        /// Ice candidates can come during several states.
        /// Anytime candidates come in, they need to be added
        /// to the peerconnection.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public virtual async Task AddRemoteIceCandidateAsync(RelayMessage message)
        {
            if (Context.PeerConnection == null)
                return;
            try
            {
                var candidates =
                    (DtoIceCandidates)JsonConvert.DeserializeObject(message.Payload, typeof(DtoIceCandidates));
                foreach (var candidate in candidates.Candidates)
                {
                    await Context.PeerConnection.AddIceCandidate(candidate.FromDto());
                }
            }
            catch (Exception)
            {
                if (Debugger.IsAttached)
                {
                    throw;
                }
            }
        }

        public virtual async Task SendLocalIceCandidatesAsync(RTCIceCandidate[] candidates)
        {
            Context.SendToPeer(RelayMessageTags.IceCandidate, JsonConvert.SerializeObject(candidates.ToDto()));
        }

        internal async Task ResumeCallVideoAsync()
        {
            if (Context.LocalVideoRenderer.IsInitialized &&
                Context.RemoteVideoRenderer.IsInitialized)
            {
                return;
            }

            Context.ResetRenderers();

            // Setup remote before local as it's more important.
            if (Context.RemoteStream != null)
            {
                var tracks = Context.RemoteStream.GetVideoTracks();
                if (tracks.Count > 0)
                {
                    var source = RtcManager.Instance.Media.CreateMediaSource(tracks[0], CallContext.PeerMediaStreamId);
                    Context.RemoteVideoRenderer.SetupRenderer(Context.ForegroundProcessId, source, Context.RemoteVideoControlSize);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Failed to resume remote video, no video track");
                }
            }
            if (Context.LocalStream != null)
            {
                var tracks = Context.LocalStream.GetVideoTracks();
                foreach (var track in tracks)
                {
                    track.Suspended = false;
                }

                if (tracks.Count > 0)
                {
                    var source = RtcManager.Instance.Media.CreateMediaSource(tracks[0], CallContext.LocalMediaStreamId);
                    Context.LocalVideoRenderer.SetupRenderer(Context.ForegroundProcessId, source, Context.LocalVideoControlSize);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Failed to resume local video, no video track");
                }
            }
        }

        internal async Task SuspendCallVideoAsync()
        {
            // Detach any renderers.
            Context.ResetRenderers();
            // Don't send RenderFormatUpdate here. The UI is suspending
            // and may not get the message.
            if (Context.LocalStream != null)
            {
                foreach (var track in Context.LocalStream.GetVideoTracks())
                {
                    track.Suspended = true;
                }
            }
        }

        public virtual async Task CameraSelectionChanged()
        {
        }

    }
}