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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation;
using Windows.Foundation.Collections;
using ChatterBox.Background.AppService;
using ChatterBox.Background.AppService.Dto;
using ChatterBox.Communication.Helpers;
using Newtonsoft.Json;

namespace ChatterBox.Background
{
    public sealed class ForegroundClient : IForegroundChannel
    {
        public IAsyncOperation<ForegroundState> GetForegroundStateAsync()
        {
            return SendToForegroundAsync<ForegroundState>();
        }

        public IAsyncOperation<string> GetShownUserIdAsync()
        {
            return SendToForegroundAsync<string>();
        }

        public IAsyncAction OnCallStatusAsync(CallStatus status)
        {
            return SendToForegroundAsync(status).AsTask().AsAsyncAction();
        }

        public IAsyncAction OnChangeMediaDevicesAsync(MediaDevicesChange mediaDevicesChange)
        {
            return SendToForegroundAsync(mediaDevicesChange).AsTask().AsAsyncAction();
        }

        public IAsyncAction OnSignaledPeerDataUpdatedAsync()
        {
            return SendToForegroundAsync().AsTask().AsAsyncAction();
        }

        public IAsyncAction OnSignaledRegistrationStatusUpdatedAsync()
        {
            return SendToForegroundAsync().AsTask().AsAsyncAction();
        }

        public IAsyncAction OnSignaledRelayMessagesUpdatedAsync()
        {
            return SendToForegroundAsync().AsTask().AsAsyncAction();
        }

        public IAsyncAction OnUpdateFrameFormatAsync(FrameFormat frameFormat)
        {
            return SendToForegroundAsync(frameFormat).AsTask().AsAsyncAction();
        }

        public IAsyncAction OnUpdateFrameRateAsync(FrameRate frameRate)
        {
            return SendToForegroundAsync(frameRate).AsTask().AsAsyncAction();
        }


        private IAsyncOperation<AppServiceResponse> SendToForegroundAsync(object arg = null,
            [CallerMemberName] string method = null)
        {
            if (Hub.Instance.ForegroundConnection == null) return Task.FromResult((AppServiceResponse)null).AsAsyncOperation();
            var channelWriteHelper = new ChannelWriteHelper(typeof(IForegroundChannel));
            var message = channelWriteHelper.FormatOutput(arg, method);
            return Hub.Instance.ForegroundConnection.SendMessageAsync(new ValueSet
            {
                {typeof (IForegroundChannel).Name, message}
            });
        }

        private IAsyncOperation<TResult> SendToForegroundAsync<TResult>(object arg = null,
            [CallerMemberName] string method = null)
            where TResult : class
        {
            return Task.Run(async () =>
            {
                var resultMessage = await SendToForegroundAsync(arg, method);
                if (resultMessage?.Status != AppServiceResponseStatus.Success) return null;
                if (!resultMessage.Message.Values.Any()) return null;
                return
                    (TResult)
                        JsonConvert.DeserializeObject(resultMessage.Message.Values.Single().ToString(), typeof(TResult));
            }).AsAsyncOperation();
        }
    }
}