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
using System.Linq;
using System.Threading.Tasks;
using ChatterBox.Background.AppService.Dto;
using ChatterBox.Background.Call.Utils;
using ChatterBox.Background.Settings;
using ChatterBox.Communication.Messages.Relay;
using Newtonsoft.Json;
using Org.WebRtc;

#pragma warning disable 1998

namespace ChatterBox.Background.Call.States
{
    internal class EstablishIncoming : BaseCallState
    {
        public EstablishIncoming()
        {
        }

        public override CallState CallState => CallState.EstablishIncoming;

        public override async Task HangupAsync()
        {
            var hangingUpState = new HangingUp();
            await Context.SwitchState(hangingUpState);
        }

        public override async Task OnEnteringStateAsync()
        {
            // We wait for the SDP.
        }

        public override async Task OnSdpOfferAsync(RelayMessage message)
        {
            bool isHold = SdpUtils.IsHold(message.Payload);
            if (isHold)
            {
                Context.VoipHelper.SetCallHeld();
            }
            else
            {
                Context.VoipHelper.SetCallActive(Context.PeerId, Context.IsVideoEnabled);
            }

            // If PeerConnection is not null, then this is an SDP renegotiation.
            if (Context.PeerConnection == null)
            {
                var config = new RTCConfiguration
                {
                    IceServers = WebRtcSettingsUtils.ToRTCIceServer(IceServerSettings.IceServers)
                };
                Context.PeerConnection = new RTCPeerConnection(config);
            }

            if(isHold)
            { 
                // Even for just a renegotiation, it's easier to just teardown the media capture and start over.
                if (Context.LocalStream != null)
                {
                    Context.PeerConnection.RemoveStream(Context.LocalStream);
                }
                Context.LocalStream?.Stop();
                Context.LocalStream = null;
                Context.RemoteStream?.Stop();
                Context.RemoteStream = null;
                Context.ResetRenderers();
            }

            MediaVideoTrack oldVideoTrack = Context.RemoteStream?.GetVideoTracks()?.FirstOrDefault();

            await Context.PeerConnection.SetRemoteDescription(new RTCSessionDescription(RTCSdpType.Offer, message.Payload));

            MediaVideoTrack newVideoTrack = Context.RemoteStream?.GetVideoTracks()?.FirstOrDefault();

            bool videoTrackChanged = oldVideoTrack != null && newVideoTrack != null
                && oldVideoTrack.Id.CompareTo(newVideoTrack.Id) != 0;

            if (videoTrackChanged)
            {
                Context.ResetRemoteRenderer();
                var source = RtcManager.Instance.Media.CreateMediaSource(newVideoTrack, CallContext.PeerMediaStreamId);
                Context.RemoteVideoRenderer.SetupRenderer(Context.ForegroundProcessId, source, Context.RemoteVideoControlSize);
            }
            else if (!isHold)
            {
                Context.LocalStream = await RtcManager.Instance.Media.GetUserMedia(new RTCMediaStreamConstraints
                {
                    videoEnabled = Context.IsVideoEnabled,
                    audioEnabled = true
                });
                Context.PeerConnection.AddStream(Context.LocalStream);

                // Setup the rendering of the local capture.
                var tracks = Context.LocalStream.GetVideoTracks();
                if (tracks.Count > 0)
                {
                    var source = RtcManager.Instance.Media.CreateMediaSource(tracks[0], CallContext.LocalMediaStreamId);
                    Context.LocalVideoRenderer.SetupRenderer(Context.ForegroundProcessId, source, Context.LocalVideoControlSize);
                }
            }
            
            var sdpAnswer = await Context.PeerConnection.CreateAnswer();
            await Context.PeerConnection.SetLocalDescription(sdpAnswer);

            var sdpVideoCodecIds = SdpUtils.GetVideoCodecIds(message.Payload);
            if (sdpVideoCodecIds.Count > 0)
            {
                Context.VideoCodecUsed = Array.Find((await Hub.Instance.MediaSettingsChannel.GetVideoCodecsAsync())?.Codecs,
                    it => it.Id == sdpVideoCodecIds.First())?.FromDto();
            }

            Context.SendToPeer(RelayMessageTags.SdpAnswer, sdpAnswer.Sdp);
            if (isHold)
            {
                await Context.SwitchState(new Held());
            }
            else
            {
                await Context.SwitchState(new Active());
            }
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
    }
}