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
using ChatterBox.Background.AppService;
using ChatterBox.Background.Settings;
using ChatterBox.Communication.Contracts;
using ChatterBox.Communication.Messages.Registration;
using System.Threading;

namespace ChatterBox.Background.Signaling
{
    public sealed class SocketConnection : ISocketConnection
    {
        private readonly IClientChannel _clientChannel;
        private readonly ISignalingSocketChannel _signalingSocketChannel;
        private static readonly SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1, 1);

        public SocketConnection(ISignalingSocketChannel signalingSocketChannel, IClientChannel clientChannel)
        {
            _clientChannel = clientChannel;
            _signalingSocketChannel = signalingSocketChannel;
        }

        public IAsyncOperation<bool> ConnectAsync()
        {
            return Task.Run(async () =>
            {
                try
                {
                    await SemaphoreSlim.WaitAsync();
                    var isConnected = await GetIsConnectedAsync();
                    if (!isConnected)
                    {
                        isConnected = (await _signalingSocketChannel.ConnectToSignalingServerAsync(null)).IsConnected;
                        if (isConnected)
                        {
                            await RegisterAsync();
                            return true;
                        }
                    }
                }
                catch
                {
                    if (Debugger.IsAttached) throw;
                }
                finally
                {
                    SemaphoreSlim.Release();
                }
                return false;
            }).AsAsyncOperation();
        }

        public IAsyncOperation<bool> DisconnectAsync()
        {
            return Task.Run(async () =>
            {
                var isConnected = await GetIsConnectedAsync();
                if (!isConnected)
                {
                    return true;
                }

                // Disconnect our connection with the Signaling Server.
                // The server will automatically recognize that the client
                // is not registered anymore, once it missed the next two heartbeats.
                await _signalingSocketChannel.DisconnectSignalingServerAsync();

                return true;
            }).AsAsyncOperation();
        }

        public IAsyncOperation<bool> GetIsConnectedAsync()
        {
            return Task.Run(async () =>
            {
                var connectionStatus = await _signalingSocketChannel.GetConnectionStatusAsync();
                return connectionStatus != null && connectionStatus.IsConnected;
            }).AsAsyncOperation();
        }

        public IAsyncAction RegisterAsync()
        {
            return Task.Run(async () =>
            {
                await _clientChannel.RegisterAsync(new Registration
                {
                    Name = RegistrationSettings.Name,
                    UserId = RegistrationSettings.UserId,
                    Domain = RegistrationSettings.Domain,
                    PushNotificationChannelURI = RegistrationSettings.PushNotificationChannelUri
                });
            }).AsAsyncAction();
        }
    }
}