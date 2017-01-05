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
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Foundation;
using ChatterBox.Communication.Contracts;
using ChatterBox.Communication.Helpers;
using ChatterBox.Communication.Messages.Peers;
using ChatterBox.Communication.Messages.Registration;
using ChatterBox.Communication.Messages.Relay;
using ChatterBox.Communication.Messages.Standard;
using ChatterBox.Server.Helpers;
using Common.Logging;

namespace ChatterBox.Server
{
    public class UnregisteredConnection : IClientChannel, IServerChannel
    {
        public delegate void OnRegisterHandler(UnregisteredConnection sender, Registration message);
        private ILog Logger => LogManager.GetLogger(ToString());

        public UnregisteredConnection(TcpClient tcpClient)
        {
            TcpClient = tcpClient;
        }

        private ChannelWriteHelper ChannelWriteHelper { get; } = new ChannelWriteHelper(typeof(IServerChannel));
        public Guid Id { get; } = Guid.NewGuid();
        public TcpClient TcpClient { get; set; }


        public IAsyncAction ClientConfirmationAsync(Confirmation confirmation)
        {
            return Task.CompletedTask.CastToAsyncAction();
        }

        public IAsyncAction ClientHeartBeatAsync()
        {
            return Task.CompletedTask.CastToAsyncAction();
        }

        public IAsyncAction GetPeerListAsync(Message message)
        {
            return Task.CompletedTask.CastToAsyncAction();
        }

        public IAsyncAction RegisterAsync(Registration message)
        {
            return Task.Run(async () =>
            {
                await ServerConfirmationAsync(Confirmation.For(message)).CastToTask();
                OnRegister?.Invoke(this, message);
            }).CastToAsyncAction();
        }

        public IAsyncAction RelayAsync(RelayMessage message)
        {
            return Task.CompletedTask.CastToAsyncAction();
        }


        public IAsyncAction OnPeerListAsync(PeerList peerList)
        {
            return Task.CompletedTask.CastToAsyncAction();
        }

        public IAsyncAction OnPeerPresenceAsync(PeerUpdate peer)
        {
            return Task.CompletedTask.CastToAsyncAction();
        }

        public IAsyncAction OnRegistrationConfirmationAsync(RegisteredReply reply)
        {
            return Task.CompletedTask.CastToAsyncAction();
        }

        public IAsyncAction ServerConfirmationAsync(Confirmation confirmation)
        {
            Write(confirmation);
            return Task.CompletedTask.CastToAsyncAction();
        }

        public IAsyncAction ServerConnectionErrorAsync()
        {
            return Task.CompletedTask.CastToAsyncAction();
        }

        public IAsyncAction ServerErrorAsync(ErrorReply reply)
        {
            return Task.CompletedTask.CastToAsyncAction();
        }

        public IAsyncAction ServerHeartBeatAsync()
        {
            return Task.CompletedTask.CastToAsyncAction();
        }

        public IAsyncAction ServerReceivedInvalidMessageAsync(InvalidMessage reply)
        {
            Write(reply);
            return Task.CompletedTask.CastToAsyncAction();
        }

        public IAsyncAction ServerRelayAsync(RelayMessage message)
        {
            return Task.CompletedTask.CastToAsyncAction();
        }


        public IAsyncAction OnInvalidRequest(InvalidMessage reply)
        {
            return Task.CompletedTask.CastToAsyncAction();
        }

        public event OnRegisterHandler OnRegister;

        public override string ToString()
        {
            return $"{nameof(UnregisteredConnection)}[{Id}]";
        }

        public void WaitForRegistration()
        {
            Task.Run(async () =>
            {
                var reader = new StreamReader(TcpClient.GetStream());
                var clientChannelProxy = new ChannelInvoker(this);

                string message;
                do
                {
                    message = await reader.ReadLineAsync();
                    Logger.Info(message);

                } while (!message.ToUpper().StartsWith(nameof(RegisterAsync).ToUpper()));

                if (!clientChannelProxy.ProcessRequest(message).Invoked)
                {
                    await OnInvalidRequest(InvalidMessage.For(message)).CastToTask();
                }
                
            });
        }

        private void Write(object arg = null, [CallerMemberName] string method = null)
        {
            var message = ChannelWriteHelper.FormatOutput(arg, method);
            var writer = new StreamWriter(TcpClient.GetStream())
            {
                AutoFlush = true
            };
            writer.WriteLine(message);
        }
    }
}