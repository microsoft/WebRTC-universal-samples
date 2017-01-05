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

namespace ChatterBox.Background
{
    public sealed class ETWEventLogger
    {
        private static volatile ETWEventLogger instance;
        private static readonly object syncRoot = new object();

        private ETWEventLogger()
        {
            if (ChatterBoxETWEventSource.Log.ConstructionException != null)
                throw ChatterBoxETWEventSource.Log.ConstructionException;
        }

        public bool ETWStatsEnabled { get; set; } = false;

        public static ETWEventLogger Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (syncRoot)
                    {
                        if (instance == null)
                        {
                            instance = new ETWEventLogger();
                        }
                    }
                }
                return instance;
            }
        }

        public void LogEvent(string shortDescription, string longDescription, string timestamp)
        {
            if (ETWStatsEnabled)
            {
                ChatterBoxETWEventSource.Log.LogEvent(shortDescription, longDescription, timestamp);
            }
        }

        public void LogEvent(string shortDescription, long timestamp)
        {
            if (ETWStatsEnabled)
            {
                ChatterBoxETWEventSource.Log.LogShortEvent(shortDescription, timestamp);
            }
        }
    }
}