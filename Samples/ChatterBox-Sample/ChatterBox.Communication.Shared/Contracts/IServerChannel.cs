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
using ChatterBox.Communication.Messages.Peers;
using ChatterBox.Communication.Messages.Registration;
using ChatterBox.Communication.Messages.Relay;
using ChatterBox.Communication.Messages.Standard;

namespace ChatterBox.Communication.Contracts
{
    public interface IServerChannel
    {
        IAsyncAction OnPeerListAsync(PeerList peerList);
        IAsyncAction OnPeerPresenceAsync(PeerUpdate peer);
        IAsyncAction OnRegistrationConfirmationAsync(RegisteredReply reply);
        IAsyncAction ServerConfirmationAsync(Confirmation confirmation);
        IAsyncAction ServerConnectionErrorAsync();
        IAsyncAction ServerErrorAsync(ErrorReply reply);
        IAsyncAction ServerHeartBeatAsync();
        IAsyncAction ServerReceivedInvalidMessageAsync(InvalidMessage reply);
        IAsyncAction ServerRelayAsync(RelayMessage message);
    }
}