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
using ChatterBox.Background.AppService.Dto;

namespace ChatterBox.Background.AppService
{
    public interface IForegroundChannel
    {
        IAsyncOperation<ForegroundState> GetForegroundStateAsync();
        IAsyncOperation<string> GetShownUserIdAsync();
        IAsyncAction OnCallStatusAsync(CallStatus status);
        IAsyncAction OnChangeMediaDevicesAsync(MediaDevicesChange mediaDevicesChange);
        IAsyncAction OnSignaledPeerDataUpdatedAsync();
        IAsyncAction OnSignaledRegistrationStatusUpdatedAsync();
        IAsyncAction OnSignaledRelayMessagesUpdatedAsync();
        IAsyncAction OnUpdateFrameFormatAsync(FrameFormat frameFormat);
        IAsyncAction OnUpdateFrameRateAsync(FrameRate frameRate);
    }
}