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

namespace ChatterBox.Background.Settings
{
    public static class SignalingSettings
    {
        public static bool AppInsightsEnabled
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey(nameof(AppInsightsEnabled)))
                {
                    return (bool) ApplicationData.Current.LocalSettings.Values[nameof(AppInsightsEnabled)];
                }
                return false;
            }
            set { ApplicationData.Current.LocalSettings.Values.AddOrUpdate(nameof(AppInsightsEnabled), value); }
        }

        public static string SignalingServerHost
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey(nameof(SignalingServerHost)))
                {
                    return (string) ApplicationData.Current.LocalSettings.Values[nameof(SignalingServerHost)];
                }
                var defaultHost = "localhost";
                SignalingServerHost = defaultHost;
                return defaultHost;
            }
            set { ApplicationData.Current.LocalSettings.Values.AddOrUpdate(nameof(SignalingServerHost), value); }
        }

        public static string SignalingServerPort
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey(nameof(SignalingServerPort)))
                {
                    return (string) ApplicationData.Current.LocalSettings.Values[nameof(SignalingServerPort)];
                }
                var defaultPort = "50000";
                SignalingServerPort = defaultPort;
                return defaultPort;
            }
            set { ApplicationData.Current.LocalSettings.Values.AddOrUpdate(nameof(SignalingServerPort), value); }
        }
    }
}