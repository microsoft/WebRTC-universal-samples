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
using ChatterBox.Background.Settings;

namespace ChatterBox.Background.Signaling.PersistedData
{
    public static class SignalingStatus
    {
        public static int Avatar
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey(nameof(Avatar)))
                {
                    return (int) ApplicationData.Current.LocalSettings.Values[nameof(Avatar)];
                }
                return 0;
            }
            set { ApplicationData.Current.LocalSettings.Values.AddOrUpdate(nameof(Avatar), value); }
        }

        public static bool IsRegistered
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey(nameof(IsRegistered)))
                {
                    return (bool) ApplicationData.Current.LocalSettings.Values[nameof(IsRegistered)];
                }
                return false;
            }
            set { ApplicationData.Current.LocalSettings.Values.AddOrUpdate(nameof(IsRegistered), value); }
        }

        private static ApplicationDataContainer SignalingStatusContainer
        {
            get
            {
                if (!ApplicationData.Current.LocalSettings.Containers.ContainsKey(nameof(SignalingStatusContainer)))
                {
                    ApplicationData.Current.LocalSettings.CreateContainer(nameof(SignalingStatusContainer),
                        ApplicationDataCreateDisposition.Always);
                }
                return ApplicationData.Current.LocalSettings.Containers[nameof(SignalingStatusContainer)];
            }
        }

        public static void Reset()
        {
            ApplicationData.Current.LocalSettings.DeleteContainer(nameof(SignalingStatusContainer));
        }
    }
}