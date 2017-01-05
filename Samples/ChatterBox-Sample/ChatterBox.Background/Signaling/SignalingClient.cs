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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;
using ChatterBox.Background.AppService;
using ChatterBox.Background.Avatars;
using ChatterBox.Background.Notifications;
using ChatterBox.Background.Signaling.PersistedData;
using ChatterBox.Communication.Contracts;
using ChatterBox.Communication.Helpers;
using ChatterBox.Communication.Messages.Peers;
using ChatterBox.Communication.Messages.Registration;
using ChatterBox.Communication.Messages.Relay;
using ChatterBox.Communication.Messages.Standard;

namespace ChatterBox.Background.Signaling
{
    public sealed class SignalingClient : IClientChannel, IServerChannel
    {
        private readonly ICallChannel _callChannel;
        private readonly IForegroundChannel _foregroundChannel;
        private readonly ISignalingSocketService _signalingSocketService;

        public SignalingClient(ISignalingSocketService signalingSocketService,
            IForegroundChannel foregroundChannel,
            ICallChannel callChannel)
        {
            _signalingSocketService = signalingSocketService;
            _callChannel = callChannel;
            _foregroundChannel = foregroundChannel;
            ServerChannelInvoker = new ChannelInvoker(this);
        }

        private ChannelWriteHelper ClientChannelWriteHelper { get; } = new ChannelWriteHelper(typeof (IClientChannel));
        private ChannelInvoker ServerChannelInvoker { get; }


        public IAsyncAction ClientConfirmationAsync(Confirmation confirmation)
        {
            return SendToServer(confirmation).AsAsyncAction();
        }

        public IAsyncAction ClientHeartBeatAsync()
        {
            try
            {
                return SendToServer().AsAsyncAction();
            }
            catch
            {
                return ServerConnectionErrorAsync();
            }
        }

        public IAsyncAction GetPeerListAsync(Message message)
        {
            return SendToServer(message).AsAsyncAction();
        }

        public IAsyncAction RegisterAsync(Registration message)
        {
            return Task.Run(async () =>
            {
                var bufferFile = await GetBufferFile();
                await bufferFile.DeleteAsync();
                await SendToServer(message);
                await GetPeerListAsync(new Message());
            }).AsAsyncAction();
        }

        public IAsyncAction RelayAsync(RelayMessage message)
        {
            return SendToServer(message).AsAsyncAction();
        }


        public IAsyncAction OnPeerListAsync(PeerList peerList)
        {
            return Task.Run(async () =>
            {
                await ClientConfirmationAsync(Confirmation.For(peerList));
                foreach (var peerStatus in peerList.Peers)
                {
                    SignaledPeerData.AddOrUpdate(peerStatus);
                }
                _foregroundChannel?.OnSignaledPeerDataUpdatedAsync();
            }).AsAsyncAction();
        }

        public IAsyncAction OnPeerPresenceAsync(PeerUpdate peer)
        {
            return Task.Run(async () =>
            {
                await ClientConfirmationAsync(Confirmation.For(peer));
                await GetPeerListAsync(new Message());
                if (DateTimeOffset.UtcNow.Subtract(peer.SentDateTimeUtc).TotalSeconds < 10)
                {
                    ToastNotificationService.ShowPresenceNotification(
                        peer.PeerData.Name,
                        AvatarLink.EmbeddedLinkFor(peer.PeerData.Avatar),
                        peer.PeerData.IsOnline);
                }
                _foregroundChannel?.OnSignaledPeerDataUpdatedAsync();
            }).AsAsyncAction();
        }

        public IAsyncAction OnRegistrationConfirmationAsync(RegisteredReply reply)
        {
            return Task.Run(async () =>
            {
                await ClientConfirmationAsync(Confirmation.For(reply));
                SignalingStatus.IsRegistered = true;
                SignalingStatus.Avatar = reply.Avatar;
                await GetPeerListAsync(new Message());
                _foregroundChannel?.OnSignaledRegistrationStatusUpdatedAsync();
            }).AsAsyncAction();
        }

        public IAsyncAction ServerConfirmationAsync(Confirmation confirmation)
        {
            return Task.CompletedTask.AsAsyncAction();
        }

        public IAsyncAction ServerConnectionErrorAsync()
        {
            return Task.Run(async () =>
            {
                SignalingStatus.IsRegistered = false;
                await _foregroundChannel.OnSignaledRegistrationStatusUpdatedAsync();
            }).AsAsyncAction();
        }

        public IAsyncAction ServerErrorAsync(ErrorReply reply)
        {
            return Task.CompletedTask.AsAsyncAction();
        }

        public IAsyncAction ServerHeartBeatAsync()
        {
            return ClientHeartBeatAsync();
        }

        public IAsyncAction ServerReceivedInvalidMessageAsync(InvalidMessage reply)
        {
            return Task.CompletedTask.AsAsyncAction();
        }

        public IAsyncAction ServerRelayAsync(RelayMessage message)
        {
            return Task.Run(async () =>
            {
                await ClientConfirmationAsync(Confirmation.For(message));
                if (message.Tag == RelayMessageTags.InstantMessage)
                {
                    await SignaledInstantMessages.AddAsync(message);
                }
                var shownUserId = await _foregroundChannel.GetShownUserIdAsync();
                if (message.Tag == RelayMessageTags.InstantMessage &&
                    !ReceivedPushNotifications.IsReceived(message.Id) &&
                    !(shownUserId != null && shownUserId.Equals(message.FromUserId)) &&
                    (DateTimeOffset.UtcNow.Subtract(message.SentDateTimeUtc).TotalMinutes < 10))
                {
                    ToastNotificationService.ShowInstantMessageNotification(message.FromName,
                        message.FromUserId, AvatarLink.EmbeddedLinkFor(message.FromAvatar), message.Payload);
                }
                _foregroundChannel?.OnSignaledRelayMessagesUpdatedAsync();

                // Handle call tags
                if (message.Tag == RelayMessageTags.Call)
                {
                    await _callChannel.OnIncomingCallAsync(message);
                }
                else if (message.Tag == RelayMessageTags.CallAnswer)
                {
                    await _callChannel.OnOutgoingCallAcceptedAsync(message);
                }
                else if (message.Tag == RelayMessageTags.CallReject)
                {
                    await _callChannel.OnOutgoingCallRejectedAsync(message);
                }
                else if (message.Tag == RelayMessageTags.SdpOffer)
                {
                    await _callChannel.OnSdpOfferAsync(message);
                }
                else if (message.Tag == RelayMessageTags.SdpAnswer)
                {
                    await _callChannel.OnSdpAnswerAsync(message);
                }
                else if (message.Tag == RelayMessageTags.IceCandidate)
                {
                    await _callChannel.OnIceCandidateAsync(message);
                }
                else if (message.Tag == RelayMessageTags.CallHangup)
                {
                    await _callChannel.OnRemoteHangupAsync(message);
                }
            }).AsAsyncAction();
        }

        public IAsyncAction HandleRequest(string request)
        {
            return Task.Run(async () =>
            {
                List<string> requests;
                // Large requests can be split into several packets.
                // We use a file to buffer requests until we can match
                // an Environment.NewLine indicating the end of a request.
                var fileExists = await BufferFileExists();
                if (fileExists)
                {
                    var bufferFile = await GetBufferFile();
                    await FileIO.AppendTextAsync(bufferFile, request);
                    requests = (await FileIO.ReadLinesAsync(bufferFile)).ToList();
                    await bufferFile.DeleteAsync();
                }
                else
                {
                    requests = request.Split(new[] { Environment.NewLine },
                        StringSplitOptions.RemoveEmptyEntries).ToList();
                }

                for (var i = 0; i < requests.Count; i++)
                {
                    // ServerChannelInvoker will invoke the function on "this" which matches
                    // the request.
                    // If the result is asynchronous, await it.
                    var invoked = ServerChannelInvoker.ProcessRequest(requests[i]);
                    if (i != requests.Count - 1) continue;
                    if (invoked.Invoked)
                    {
                        var invokeResult = invoked.Result;
                        var asyncAction = invokeResult as IAsyncAction;
                        if (asyncAction != null)
                        {
                            await asyncAction;
                        }
                        continue;
                    }
                    // Invocation failed, probably because the request string is partial.
                    // Put it back in the buffer file to wait for more packets.
                    var bufferFile = await GetBufferFile();
                    await FileIO.AppendTextAsync(bufferFile, requests[i]);
                }
            }).AsAsyncAction();
        }


        private IAsyncOperation<bool> BufferFileExists()
        {
            return
                Task.Run(
                    async () =>
                    {
                        return
                            (await ApplicationData.Current.LocalFolder.GetFilesAsync()).Any(s => s.Name == "BufferFile");
                    }).AsAsyncOperation();
        }

        private IAsyncOperation<StorageFile> GetBufferFile()
        {
            return ApplicationData.Current.LocalFolder.CreateFileAsync("BufferFile",
                CreationCollisionOption.OpenIfExists);
        }

        private async Task SendToServer(object arg = null, [CallerMemberName] string method = null)
        {
            var message = ClientChannelWriteHelper.FormatOutput(arg, method);

            using (var socketOperation = _signalingSocketService.SocketOperation)
            {
                var socket = socketOperation.Socket;
                if (socket != null)
                {
                    using (var writer = new DataWriter(socket.OutputStream))
                    {
                        writer.WriteString($"{message}{Environment.NewLine}");
                        await writer.StoreAsync();
                        await writer.FlushAsync();
                        writer.DetachStream();
                    }
                }
            }
        }
    }
}