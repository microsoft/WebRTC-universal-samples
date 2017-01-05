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
using Windows.Networking.PushNotifications;
using ChatterBox.Background.Avatars;
using ChatterBox.Background.Notifications;
using ChatterBox.Background.Signaling.PersistedData;
using ChatterBox.Communication.Messages.Relay;
using Newtonsoft.Json;

namespace ChatterBox.Background.Tasks
{
    public sealed class PushNotificationTask : IBackgroundTask
    {
        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            using (new BackgroundTaskDeferralWrapper(taskInstance.GetDeferral()))
            {
                try
                {
                    var rawNotification = (RawNotification) taskInstance.TriggerDetails;
                    var rawContent = rawNotification.Content;

                    var serializedParameter =
                        rawContent.Substring(rawContent.IndexOf(" ", StringComparison.CurrentCultureIgnoreCase) + 1);
                    var type = typeof (RelayMessage);
                    var message = (RelayMessage) JsonConvert.DeserializeObject(serializedParameter, type);

                    if (message == null) return;

                    var isTimeout = (DateTimeOffset.UtcNow - message.SentDateTimeUtc).TotalSeconds > 60;

                    if (message.Tag == RelayMessageTags.Call)
                    {
                        if (isTimeout) return;
                        ToastNotificationService.ShowInstantMessageNotification(message.FromName, message.FromUserId,
                            AvatarLink.EmbeddedLinkFor(message.FromAvatar),
                            $"Missed call at {message.SentDateTimeUtc.ToLocalTime()}.");
                    }
                    else if (message.Tag == RelayMessageTags.InstantMessage)
                    {
                        if (isTimeout || await SignaledInstantMessages.IsReceivedAsync(message.Id)) return;
                        ToastNotificationService.ShowInstantMessageNotification(message.FromName, message.FromUserId,
                            AvatarLink.EmbeddedLinkFor(message.FromAvatar), message.Payload);
                        ReceivedPushNotifications.Add(message.Id);
                        await SignaledInstantMessages.AddAsync(message);
                    }
                }
                catch (Exception)
                {
                }
            }
        }
    }
}