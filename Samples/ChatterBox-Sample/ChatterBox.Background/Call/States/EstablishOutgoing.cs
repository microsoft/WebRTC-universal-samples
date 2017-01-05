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
    internal class EstablishOutgoing : BaseCallState
    {
        public enum Reason
        {
            EstablishCall,
            HoldCall,
            SwitchCamera                        
        };

        private readonly Reason _reason;

        public EstablishOutgoing(Reason reason)
        {
            _reason = reason;
        }

        public override CallState CallState => CallState.EstablishOutgoing;

        public override async Task HangupAsync()
        {
            var hangingUpState = new HangingUp();
            await Context.SwitchState(hangingUpState);
        }

        public override async Task OnEnteringStateAsync()
        {
            if (_reason == Reason.HoldCall)
            {
                Context.VoipHelper.SetCallHeld();
            }
            else if (Context.VoipHelper.HasCall())
            {
                Context.VoipHelper.SetCallActive(Context.PeerId, Context.IsVideoEnabled);
            }
            else
            {
                Context.VoipHelper.StartOutgoingCall(Context.PeerId, Context.IsVideoEnabled);
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

            switch (_reason)
            {
                case Reason.HoldCall:
                {
                    // Even for just a renegotiation, it's easier to just teardown the media capture and start over.
                    if (Context.LocalStream != null)
                    {
                        Context.PeerConnection.RemoveStream(Context.LocalStream);
                    }
                    if (Context.RemoteStream != null)
                    {
                        Context.PeerConnection.RemoveStream(Context.RemoteStream);
                    }
                    foreach (var stream in Context.PeerConnection.GetLocalStreams())
                    {
                        Debug.WriteLine("Streams remaining.");
                    }

                    Context.LocalStream?.Stop();
                    Context.LocalStream = null;
                    Context.RemoteStream?.Stop();
                    Context.RemoteStream = null;
                    Context.ResetRenderers();
                    break;
                }

                case Reason.EstablishCall:
                {
                    Context.LocalStream?.Stop();
                    Context.LocalStream = null;
                    Context.RemoteStream?.Stop();
                    Context.RemoteStream = null;
                    Context.ResetRenderers();

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
                        Context.LocalVideoRenderer.SetupRenderer(Context.ForegroundProcessId, source,Context.LocalVideoControlSize);
                    }
                    break;
                }
                case Reason.SwitchCamera:
                {
                    var videoDevice = await Context.MediaSettingsChannel.GetVideoDeviceAsync();
                    // We expect the camera we want to switch to is selected.
                    if (videoDevice == null)
                    {
                        // This could happen only in an error scenario and would have the
                        // effect of loosing the video.
                        // Since will not be able to continue the video call, just end the call.
                        await Context.SwitchState(new HangingUp());
                        return;
                    }
                    RtcManager.Instance.Media.SelectVideoDevice(videoDevice.FromDto());

                    // Remove old video track.
                    var videoTracks = Context.LocalStream.GetVideoTracks();
                    bool videoTrackEnabled = true;
                    if (videoTracks.Count > 0)
                    {
                        var oldVideoTrack = videoTracks[0];
                        videoTrackEnabled = oldVideoTrack.Enabled;
                        oldVideoTrack.Stop();
                        Context.LocalStream.RemoveTrack(oldVideoTrack);
                    }
                    videoTracks.Clear();
                    Context.ResetLocalRenderer();

                    // Create new stream having a new video track.
                    var newStream = await RtcManager.Instance.Media.GetUserMedia(new RTCMediaStreamConstraints
                    {
                        videoEnabled = true,
                        audioEnabled = false
                    });

                    // Move the new video track from new stream to old stream.
                    var newVideoTrack = newStream.GetVideoTracks().First();
                    newVideoTrack.Enabled = videoTrackEnabled;
                    Context.LocalStream.AddTrack(newVideoTrack);

                    newStream.RemoveTrack(newVideoTrack);
                    newStream.Stop();

                    var source = RtcManager.Instance.Media.CreateMediaSource(newVideoTrack, CallContext.LocalMediaStreamId);
                    Context.LocalVideoRenderer.SetupRenderer(Context.ForegroundProcessId, source, Context.LocalVideoControlSize);
                    break;
                }
            }

            var sdpOffer = await Context.PeerConnection.CreateOffer();
            var sdpString = sdpOffer.Sdp;

            Org.WebRtc.CodecInfo videoCodecToUse = null;
            // In case of camera switch, try to use the codec from the call's first SDP negotiation.
            if (_reason == Reason.SwitchCamera && Context.VideoCodecUsed != null)
            {
                videoCodecToUse = Context.VideoCodecUsed;
            }
            else
            {
                videoCodecToUse = (await Hub.Instance.MediaSettingsChannel.GetVideoCodecAsync()).FromDto();
            }
            
            SdpUtils.SelectCodecs(ref sdpString,
                (await Hub.Instance.MediaSettingsChannel.GetAudioCodecAsync()).FromDto(),
                videoCodecToUse);
            
            if(_reason == Reason.EstablishCall)
            {
                Context.VideoCodecUsed = videoCodecToUse;
            }

            sdpOffer.Sdp = sdpString;
            await Context.PeerConnection.SetLocalDescription(sdpOffer);

            Context.SendToPeer(RelayMessageTags.SdpOffer, sdpOffer.Sdp);
        }

        public override async Task OnSdpAnswerAsync(RelayMessage message)
        {
            await Context.PeerConnection.SetRemoteDescription(new RTCSessionDescription(RTCSdpType.Answer, message.Payload));
            if (SdpUtils.IsHold(message.Payload))
            {
                await Context.SwitchState(new Held());
            }
            else
            {
                await Context.SwitchState(new Active());
            }
        }

        public override async Task RemoteHangupAsync(RelayMessage message)
        {
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
    }
}