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
using System.Collections.Generic;
using ChatterBox.Background.Settings;
using Org.WebRtc;

namespace ChatterBox.Background.Call.Utils
{
    internal class WebRtcSettingsUtils
    {
        public static List<RTCIceServer> ToRTCIceServer(IEnumerable<IceServer> iceServerList)
        {
            if (iceServerList == null) throw new ArgumentNullException(nameof(iceServerList));

            var rtcList = new List<RTCIceServer>();
            foreach (var iceServer in iceServerList)
            {
                rtcList.Add(new RTCIceServer
                {
                    Url = iceServer.Url,
                    Username = iceServer.Username ?? string.Empty,
                    Credential = iceServer.Password ?? string.Empty
                });
            }

            return rtcList;
        }
    }
}