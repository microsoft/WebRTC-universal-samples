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
    public static class SignallingSettings
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

        public static string SignallingServerHost
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey(nameof(SignallingServerHost)))
                {
                    return (string) ApplicationData.Current.LocalSettings.Values[nameof(SignallingServerHost)];
                }
                var defaultHost = "localhost";
                SignallingServerHost = defaultHost;
                return defaultHost;
            }
            set { ApplicationData.Current.LocalSettings.Values.AddOrUpdate(nameof(SignallingServerHost), value); }
        }

        public static string SignallingServerPort
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey(nameof(SignallingServerPort)))
                {
                    return (string) ApplicationData.Current.LocalSettings.Values[nameof(SignallingServerPort)];
                }
                var defaultPort = "50000";
                SignallingServerPort = defaultPort;
                return defaultPort;
            }
            set { ApplicationData.Current.LocalSettings.Values.AddOrUpdate(nameof(SignallingServerPort), value); }
        }
    }
}