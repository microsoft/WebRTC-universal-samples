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

using Windows.Storage;

namespace ChatterBox.Background.Signalling.PersistedData
{
    public static class ReceivedPushNotifications
    {
        private static ApplicationDataContainer ReceivedPushNotificationsContainer
        {
            get
            {
                if (
                    !ApplicationData.Current.LocalSettings.Containers.ContainsKey(
                        nameof(ReceivedPushNotificationsContainer)))
                {
                    ApplicationData.Current.LocalSettings.CreateContainer(nameof(ReceivedPushNotificationsContainer),
                        ApplicationDataCreateDisposition.Always);
                }
                return ApplicationData.Current.LocalSettings.Containers[nameof(ReceivedPushNotificationsContainer)];
            }
        }

        public static void Add(string messageID)
        {
            if (messageID != null)
            {
                ReceivedPushNotificationsContainer.CreateContainer(messageID, ApplicationDataCreateDisposition.Always);
            }
        }

        public static bool IsReceived(string messageID)
        {
            var ret = false;

            if (messageID != null)
            {
                ret = ReceivedPushNotificationsContainer.Containers.ContainsKey(messageID);
                if (ret)
                    ReceivedPushNotificationsContainer.DeleteContainer(messageID);
            }
            return ret;
        }
    }
}