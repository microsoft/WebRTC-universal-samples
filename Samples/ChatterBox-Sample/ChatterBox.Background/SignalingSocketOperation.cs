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

using System.Diagnostics;
using System.Threading;
using Windows.Networking.Sockets;
using ChatterBox.Background.Signalling;

namespace ChatterBox.Background
{
    public sealed class SignallingSocketOperation : ISignallingSocketOperation
    {
        private static readonly SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1, 1);
        private StreamSocket _socket;

        internal SignallingSocketOperation()
        {
            SemaphoreSlim.Wait();
        }

        public static string SignallingSocketId { get; } = nameof(SignallingSocketId);


        public void Dispose()
        {
            _socket?.TransferOwnership(SignallingSocketId);
            SemaphoreSlim.Release();
        }

        public void Disconnect()
        {
            Dispose();
            if (Socket == null) return;
            _socket.Dispose();
            _socket = null;
        }

        public StreamSocket Socket
        {
            get
            {
                if (_socket != null) return _socket;

                try
                {
                    SocketActivityInformation socketInformation;
                    _socket = SocketActivityInformation.AllSockets.TryGetValue(SignallingSocketId, out socketInformation)
                        ? socketInformation.StreamSocket
                        : null;
                }
                catch (System.Exception exception)
                {
                    Debug.WriteLine("SignallingSocketOperation exception: " + exception.Message);
                    _socket = null;
                }

                if (_socket == null)
                {
                    Debug.WriteLine("SignallingSocketOperation - Socket was null");
                }

                return _socket;
            }
        }
    }
}