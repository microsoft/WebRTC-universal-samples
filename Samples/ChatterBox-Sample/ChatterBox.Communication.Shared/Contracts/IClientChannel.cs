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
using ChatterBox.Communication.Messages.Registration;
using ChatterBox.Communication.Messages.Relay;
using ChatterBox.Communication.Messages.Standard;

namespace ChatterBox.Communication.Contracts
{
    public interface IClientChannel
    {
        IAsyncAction ClientConfirmationAsync(Confirmation confirmation);
        IAsyncAction ClientHeartBeatAsync();
        IAsyncAction GetPeerListAsync(Message message);
        IAsyncAction RegisterAsync(Registration message);
        IAsyncAction RelayAsync(RelayMessage message);
    }
}