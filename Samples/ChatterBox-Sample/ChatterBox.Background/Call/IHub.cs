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

using System.Collections.Generic;
using System.Threading.Tasks;
using ChatterBox.Background.AppService.Dto;
using ChatterBox.Communication.Messages.Relay;
using Org.WebRtc;

namespace ChatterBox.Background.Call
{
    internal interface IHub
    {
        bool IsAppInsightsEnabled { get; set; }

        void InitialiazeStatsManager(RTCPeerConnection pc);

        Task OnCallStatusAsync(CallStatus callStatus);

        Task OnChangeMediaDevices(MediaDevicesChange mediaDeviceChange);

        Task OnUpdateFrameFormat(FrameFormat frameFormat);

        Task OnUpdateFrameRate(FrameRate frameRate);
        Task Relay(RelayMessage message);

        void StartStatsManagerCallWatch();

        void StopStatsManagerCallWatch();

        void ToggleStatsManagerConnectionState(bool enable);

        void TrackStatsManagerEvent(string name, IDictionary<string, string> props);

        void TrackStatsManagerMetric(string name, double value);
    }
}