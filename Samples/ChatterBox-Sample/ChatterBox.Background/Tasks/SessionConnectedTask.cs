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
using Windows.ApplicationModel.Background;
using ChatterBox.Background.AppService.Dto;
using ChatterBox.Background.Settings;
using ChatterBox.Communication.Messages.Registration;

namespace ChatterBox.Background.Tasks
{
    public sealed class SessionConnectedTask : IBackgroundTask
    {
        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            var deferral = taskInstance.GetDeferral();
            try
            {
                var taskHelper = new TaskHelper();

                var signallingTask = taskHelper.GetTask(nameof(SignallingTask));

                var connOwner = new ConnectionOwner
                {
                    OwnerId = signallingTask.TaskId.ToString()
                };

                await Hub.Instance.SignallingSocketChannel.ConnectToSignallingServerAsync(connOwner);

                await Hub.Instance.SignallingClient.RegisterAsync(new Registration
                {
                    Name = RegistrationSettings.Name,
                    UserId = RegistrationSettings.UserId,
                    Domain = RegistrationSettings.Domain,
                    PushNotificationChannelURI = RegistrationSettings.PushNotificationChannelUri
                });
            }
            finally
            {
                deferral.Complete();
            }
        }
    }
}