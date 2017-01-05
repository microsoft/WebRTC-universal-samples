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
using ChatterBox.Background.AppService.Dto;

namespace ChatterBox.Services
{
    public interface IForegroundUpdateService
    {
        event Func<string> GetShownUser;
        event Action<CallStatus> OnCallStatusUpdate;
        event Action<FrameFormat> OnFrameFormatUpdate;
        event Action<FrameRate> OnFrameRateUpdate;
        event Action<MediaDevicesChange> OnMediaDevicesChanged;
        event Action OnPeerDataUpdated;
        event Action OnRegistrationStatusUpdated;
        event Action OnRelayMessagesUpdated;
    }
}