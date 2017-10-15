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
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Networking;
using Windows.Networking.Sockets;
using ChatterBox.Background.AppService;
using ChatterBox.Background.AppService.Dto;
using ChatterBox.Background.Settings;
using ChatterBox.Background.Signalling;
using ChatterBox.Background.Signalling.PersistedData;

namespace ChatterBox.Background
{
    public sealed class SignallingSocketChannel : ISignallingSocketService, ISignallingSocketChannel
    {
        public IAsyncOperation<ConnectionStatus> ConnectToSignallingServerAsync(ConnectionOwner connectionOwner)
        {
            return Task.Run(async () =>
            {
                try
                {
                    SignaledPeerData.Reset();
                    SignallingStatus.Reset();
                    await SignaledInstantMessages.ResetAsync();

                    var socket = new StreamSocket();
                    socket.EnableTransferOwnership(Guid.Parse(connectionOwner.OwnerId),
                        SocketActivityConnectedStandbyAction.Wake);

                    var connectCancellationTokenSource = new CancellationTokenSource(2000);
                    var connectAsync = socket.ConnectAsync(new HostName(SignallingSettings.SignallingServerHost),
                        SignallingSettings.SignallingServerPort, SocketProtectionLevel.PlainSocket);
                    var connectTask = connectAsync.AsTask(connectCancellationTokenSource.Token);
                    await connectTask;

                    socket.TransferOwnership(SignallingSocketOperation.SignallingSocketId);
                    return new ConnectionStatus
                    {
                        IsConnected = true
                    };
                }
                catch (Exception exception)
                {
                    Debug.WriteLine("Failed to connect to signalling server: ex: " + exception.Message);
                    return new ConnectionStatus
                    {
                        IsConnected = false
                    };
                }
            }).AsAsyncOperation();
        }

        public IAsyncAction DisconnectSignallingServerAsync()
        {
            return Task.Run(async () =>
            {
                SocketOperation.Disconnect();
                SignaledPeerData.Reset();
                SignallingStatus.Reset();
                await SignaledInstantMessages.ResetAsync();
            }).AsAsyncAction();
            
        }

        public IAsyncOperation<ConnectionStatus> GetConnectionStatusAsync()
        {
            using (var socketOperation = SocketOperation)
            {
                return Task.FromResult(new ConnectionStatus
                {
                    IsConnected = socketOperation.Socket != null
                }).AsAsyncOperation();
            }
        }

        public ISignallingSocketOperation SocketOperation => new SignallingSocketOperation();
    }
}