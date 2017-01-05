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
using Windows.Storage;

namespace ChatterBox.Background.Settings
{
    public static class RegistrationSettings
    {
        public static string Domain
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey(nameof(Domain)))
                {
                    return (string) ApplicationData.Current.LocalSettings.Values[nameof(Domain)];
                }
                const string defaultDomain = "chatterbox.microsoft.com";
                Domain = defaultDomain;
                return defaultDomain;
            }
            set { ApplicationData.Current.LocalSettings.Values.AddOrUpdate(nameof(Domain), value); }
        }

        public static string Name
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey(nameof(Name)))
                {
                    return (string) ApplicationData.Current.LocalSettings.Values[nameof(Name)];
                }
                return null;
            }
            set { ApplicationData.Current.LocalSettings.Values.AddOrUpdate(nameof(Name), value); }
        }

        public static string PushNotificationChannelUri
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey(nameof(PushNotificationChannelUri)))
                {
                    return (string) ApplicationData.Current.LocalSettings.Values[nameof(PushNotificationChannelUri)];
                }
                return null;
            }
            set { ApplicationData.Current.LocalSettings.Values.AddOrUpdate(nameof(PushNotificationChannelUri), value); }
        }

        public static string UserId
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey(nameof(UserId)))
                {
                    return (string) ApplicationData.Current.LocalSettings.Values[nameof(UserId)];
                }
                var newUserId = Guid.NewGuid().ToString();
                UserId = newUserId;
                return newUserId;
            }
            set { ApplicationData.Current.LocalSettings.Values.AddOrUpdate(nameof(UserId), value); }
        }

        public static void Reset()
        {
            ApplicationData.Current.LocalSettings.Values.Remove(nameof(Domain));
            ApplicationData.Current.LocalSettings.Values.Remove(nameof(Name));
            ApplicationData.Current.LocalSettings.Values.Remove(nameof(UserId));
            ApplicationData.Current.LocalSettings.Values.Remove(nameof(PushNotificationChannelUri));
        }
    }
}