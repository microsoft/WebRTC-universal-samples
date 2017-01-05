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

using System.Diagnostics;
using System.Diagnostics.Tracing;

namespace ChatterBox.Background
{
    internal sealed class ChatterBoxETWEventSource : EventSource
    {
        public class Keywords
        {
            public const EventKeywords Diagnostic = (EventKeywords)1;
        }

        private const int longEventId = 108; // 1-107 used by webrtc events provider
        private const int shortEventId = 109;

        public static ChatterBoxETWEventSource Log = new ChatterBoxETWEventSource();

        [Event(longEventId, Keywords = Keywords.Diagnostic)]
        public void LogEvent(string shortDescription, string longDescription, string timestamp)
        {
            WriteEvent(longEventId, shortDescription, longDescription, timestamp);
            Debug.WriteLine("!!!shortDescription = " + shortDescription +
                            "\n longDescription = " + longDescription);
        }

        [Event(shortEventId, Keywords = Keywords.Diagnostic)]
        public void LogShortEvent(string shortDescription, long timestamp)
        {
            WriteEvent(shortEventId, shortDescription, timestamp);
            Debug.WriteLine("!!!shortDescription = " + shortDescription);
        }
    }
}