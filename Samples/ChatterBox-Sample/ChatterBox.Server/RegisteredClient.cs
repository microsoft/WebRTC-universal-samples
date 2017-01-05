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
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Foundation;
using ChatterBox.Communication.Contracts;
using ChatterBox.Communication.Helpers;
using ChatterBox.Communication.Messages.Interfaces;
using ChatterBox.Communication.Messages.Peers;
using ChatterBox.Communication.Messages.Registration;
using ChatterBox.Communication.Messages.Relay;
using ChatterBox.Communication.Messages.Standard;
using ChatterBox.Server.Helpers;
using Common.Logging;

namespace ChatterBox.Server
{
    public class RegisteredClient : IClientChannel, IServerChannel
    {
        private PushNotificationSender _pushNotificationSender;

        public RegisteredClient()
        {
            ClientReadProxy = new ChannelInvoker(this);
        }

        private TcpClient ActiveConnection { get; set; }
        public int Avatar { get; set; }
        private ChannelWriteHelper ChannelWriteHelper { get; } = new ChannelWriteHelper(typeof (IServerChannel));
        private ChannelInvoker ClientReadProxy { get; }
        private Guid ConnectionId { get; set; }
        public string Domain { get; set; }
        public bool IsOnline { get; private set; }
        private ILog Logger => LogManager.GetLogger(ToString());

        private ConcurrentQueue<RegisteredClientMessageQueueItem> MessageQueue { get; set; } =
            new ConcurrentQueue<RegisteredClientMessageQueueItem>();

        public string Name { get; set; }

        public string UserId { get; set; }
        private ConcurrentQueue<string> WriteQueue { get; set; } = new ConcurrentQueue<string>();


        public IAsyncAction ClientConfirmationAsync(Confirmation confirmation)
        {
            var message = MessageQueue.SingleOrDefault(s => s.Message.Id == confirmation.ConfirmationFor);
            if (message != null)
            {
                message.IsDelivered = true;
            }
            return Task.CompletedTask.CastToAsyncAction();
        }

        public IAsyncAction ClientHeartBeatAsync()
        {
            return Task.CompletedTask.CastToAsyncAction();
        }

        public IAsyncAction GetPeerListAsync(Message message)
        {
            OnGetPeerList?.Invoke(this, message);
            return Task.CompletedTask.CastToAsyncAction();
        }

        public IAsyncAction RegisterAsync(Registration message)
        {
            return Task.CompletedTask.CastToAsyncAction();
        }

        public IAsyncAction RelayAsync(RelayMessage message)
        {
            return Task.Run(async () =>
            {
                await ServerConfirmationAsync(Confirmation.For(message)).CastToTask();
                OnRelayMessage?.Invoke(this, message);
            }).CastToAsyncAction();
        }


        public IAsyncAction OnPeerListAsync(PeerList peerList)
        {
            return EnqueueMessage(peerList).CastToAsyncAction();
        }

        public IAsyncAction OnPeerPresenceAsync(PeerUpdate peer)
        {
            return EnqueueMessage(peer).CastToAsyncAction();
        }

        public IAsyncAction OnRegistrationConfirmationAsync(RegisteredReply reply)
        {
            return EnqueueMessage(reply).CastToAsyncAction();
        }

        public IAsyncAction ServerConfirmationAsync(Confirmation confirmation)
        {
            EnqueueOutput(confirmation);
            return Task.CompletedTask.CastToAsyncAction();
        }

        public IAsyncAction ServerConnectionErrorAsync()
        {
            return Task.CompletedTask.CastToAsyncAction();
        }

        public IAsyncAction ServerErrorAsync(ErrorReply reply)
        {
            return EnqueueMessage(reply).CastToAsyncAction();
        }

        public IAsyncAction ServerHeartBeatAsync()
        {
            if (ActiveConnection != null)
            {
                EnqueueOutput();
            }
            return Task.CompletedTask.CastToAsyncAction();
        }

        public IAsyncAction ServerReceivedInvalidMessageAsync(InvalidMessage reply)
        {
            EnqueueOutput(reply);
            return Task.CompletedTask.CastToAsyncAction();
        }

        public IAsyncAction ServerRelayAsync(RelayMessage message)
        {
            return EnqueueMessage(message).CastToAsyncAction();
        }

        public event Action<RegisteredClient> OnConnected;
        public event Action<RegisteredClient> OnDisconnected;
        public event Action<RegisteredClient, IMessage> OnGetPeerList;
        public event Action<RegisteredClient, RelayMessage> OnRelayMessage;

        public bool RegisterClientForPushNotifications(string channelUri)
        {
            if (string.IsNullOrEmpty(channelUri)) return false;
            _pushNotificationSender = new PushNotificationSender();
            _pushNotificationSender.OnChannelUriExpired += OnUserChannelUriExpired;
            _pushNotificationSender.ChannelUri = channelUri;
            return true;
        }

        public async Task SetActiveConnectionAsync(UnregisteredConnection connection, Registration message)
        {
            Logger.Debug("Handling new TCP connection.");

            ConnectionId = Guid.NewGuid();
            ActiveConnection = connection.TcpClient;

            RegisterClientForPushNotifications(message.PushNotificationChannelURI);

            await OnRegistrationConfirmationAsync(new RegisteredReply
            {
                Avatar = Avatar,
                ReplyFor = message.Id
            }).CastToTask();
            ResetQueues();
            StartReading();
            StartWriting();
            IsOnline = true;
            StartMessageQueueProcessing();
            OnConnected?.Invoke(this);
        }

        public override string ToString()
        {
            return $"[{Domain}/{Name}]";
        }


        private async Task EnqueueMessage(IMessage message, [CallerMemberName] string method = null)
        {
            var serializedString = ChannelWriteHelper.FormatOutput(message, method);
            var queueItem = new RegisteredClientMessageQueueItem
            {
                SerializedMessage = serializedString,
                Message = message,
                Method = method
            };

            if (ActiveConnection == null)
            {
                if (_pushNotificationSender != null)
                {
                    await _pushNotificationSender.SendNotificationAsync(queueItem.SerializedMessage);
                }

            }

            MessageQueue.Enqueue(queueItem);
        }

        private void EnqueueOutput(object message = null, [CallerMemberName] string method = null)
        {
            WriteQueue.Enqueue(ChannelWriteHelper.FormatOutput(message, method));
        }

        private async Task OnTcpClientDisconnected(Guid oldConnectionId)
        {
            if (oldConnectionId == ConnectionId)
            {
                var itemsToSend = MessageQueue.ToList();

                ActiveConnection = null;
                if (!IsOnline) return;
                IsOnline = false;
                OnDisconnected?.Invoke(this);

                if (_pushNotificationSender != null)
                {
                    foreach (var item in itemsToSend)
                    {
                        await _pushNotificationSender.SendNotificationAsync(item.SerializedMessage);
                    }
                }
            }
        }

        private void OnUserChannelUriExpired()
        {
            _pushNotificationSender.ChannelUri = null;
        }

        private void ResetQueues()
        {
            WriteQueue = new ConcurrentQueue<string>();
            var queuedMessages = MessageQueue.OrderBy(s => s.Message.SentDateTimeUtc).ToList();
            MessageQueue = new ConcurrentQueue<RegisteredClientMessageQueueItem>();

            var confirmationMessage = queuedMessages.Last(s => s.Method == nameof(OnRegistrationConfirmationAsync));
            queuedMessages.RemoveAll(s => s.Method == nameof(OnRegistrationConfirmationAsync));
            queuedMessages.Insert(0, confirmationMessage);

            foreach (var queuedMessage in queuedMessages)
            {
                queuedMessage.IsSent = false;
                queuedMessage.IsDelivered = false;
                MessageQueue.Enqueue(queuedMessage);
            }
        }

        private void StartMessageQueueProcessing()
        {
            Task.Run(async () =>
            {
                while (IsOnline)
                {
                    while (!MessageQueue.IsEmpty)
                    {
                        RegisteredClientMessageQueueItem item;
                        if (!MessageQueue.TryPeek(out item)) break;

                        if (!item.IsSent)
                        {
                            item.IsSent = true;
                            WriteQueue.Enqueue(item.SerializedMessage);
                        }
                        else
                        {
                            if (!item.IsDelivered) break;
                            if (!MessageQueue.TryDequeue(out item)) break;
                        }
                    }

                    await Task.Delay(10);
                }
            });
        }

        private void StartReading()
        {
            Task.Run(async () =>
            {
                var connectionId = ConnectionId;
                try
                {
                    var reader = new StreamReader(ActiveConnection.GetStream());
                    while (IsOnline && connectionId == ConnectionId)
                    {
                        var message = await reader.ReadLineAsync();
                        if (message == null) break;
                        Logger.Trace($">> {message}");
                        if (!ClientReadProxy.ProcessRequest(message).Invoked)
                        {
                            await ServerReceivedInvalidMessageAsync(InvalidMessage.For(message)).CastToTask();
                        }
                    }
                }
                catch (Exception exception)
                {
                    Logger.Warn($"[READ] Disconnected. Reason: {exception.Message}");
                    await OnTcpClientDisconnected(connectionId);
                }
            });
        }

        private void StartWriting()
        {
            Task.Run(async () =>
            {
                var connectionId = ConnectionId;
                try
                {
                    var writer = new StreamWriter(ActiveConnection.GetStream())
                    {
                        AutoFlush = true
                    };
                    while (IsOnline && connectionId == ConnectionId)
                    {
                        while (!WriteQueue.IsEmpty)
                        {
                            string message;
                            if (!WriteQueue.TryDequeue(out message)) continue;

                            Logger.Debug($"<< {message}");
                            await writer.WriteLineAsync(message);
                        }
                        await Task.Delay(10);
                    }
                }
                catch (Exception exception)
                {
                    Logger.Warn($"[WRITE] Disconnected. Reason: {exception.Message}");
                    await OnTcpClientDisconnected(connectionId);
                }
            });
        }
    }
}