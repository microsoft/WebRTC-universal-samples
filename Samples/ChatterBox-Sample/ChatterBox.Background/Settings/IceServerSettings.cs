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
using Windows.Storage;
using ChatterBox.Background.Helpers;

namespace ChatterBox.Background.Settings
{
    public sealed class IceServerSettings
    {
        public static IEnumerable<IceServer> IceServers
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey(nameof(IceServers)))
                {
                    var str = ApplicationData.Current.LocalSettings.Values[nameof(IceServers)];
                    return XmlSerializationHelper.FromXml<List<IceServer>>((string) str);
                }
                var defaultList = GetDefaultList();
                IceServers = defaultList;
                return defaultList;
            }
            set
            {
                var list = new List<IceServer>(value);
                var str = XmlSerializationHelper.ToXml(list);
                ApplicationData.Current.LocalSettings.Values.AddOrUpdate(nameof(IceServers), str);
            }
        }

        private static List<IceServer> GetDefaultList()
        {
            return new List<IceServer>
            {
                new IceServer
                {
                    Url = "stun:stun.l.google.com:19302"
                },
                new IceServer
                {
                    Url = "stun:stun1.l.google.com:19302"
                },
                new IceServer
                {
                    Url = "stun:stun2.l.google.com:19302"
                },
                new IceServer
                {
                    Url = "stun:stun3.l.google.com:19302"
                },
                new IceServer
                {
                    Url = "stun:stun4.l.google.com:19302"
                },
                new IceServer
                {
                    Url = "turn:40.76.194.255:3478",
                    Username = "testrtc",
                    Password = "rtc123"
                }
            };
        }
    }
}