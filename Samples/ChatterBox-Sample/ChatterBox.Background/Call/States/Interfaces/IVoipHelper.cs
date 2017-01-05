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

using System.Threading.Tasks;
using ChatterBox.Background.AppService.Dto;
using ChatterBox.Communication.Messages.Relay;

namespace ChatterBox.Background.Call.States.Interfaces
{
    /// <summary>
    ///     Voip call helper.
    /// </summary>
    internal interface IVoipHelper
    {
        bool HasCall();

        /// <summary>
        /// Sets the current call as active or creates a new
        /// one and sets it as active.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="videoEnabled"></param>
        void SetCallActive(string userId, bool videoEnabled);
        void SetCallHeld();

        Task StartIncomingCallAsync(RelayMessage message);
        void StartOutgoingCall(string userId, bool videoEnabled);

        Task StartVoipTask();

        void StopVoip();
    }

}