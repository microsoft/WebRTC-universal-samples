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
using System.Diagnostics;
using Windows.ApplicationModel.Background;

namespace ChatterBox.Background.Tasks
{
    public sealed class VoipTask : IBackgroundTask
    {
        private BackgroundTaskDeferral _deferral;


        public void Run(IBackgroundTaskInstance taskInstance)
        {
            try
            {
                if (Hub.Instance.VoipTaskInstance != null)
                {
                    Debug.WriteLine("VoipTask already started.");
                    return;
                }

                _deferral = taskInstance.GetDeferral();
                Hub.Instance.VoipTaskInstance = this;
                Debug.WriteLine($"{DateTime.Now} VoipTask started.");
                taskInstance.Canceled += (s, e) => CloseVoipTask();
            }
            catch (Exception e)
            {
                _deferral?.Complete();
                if (Hub.Instance.IsAppInsightsEnabled)
                {
                    Hub.Instance.RtcStatsManager.TrackException(e);
                }
                throw;
            }
        }


        public void CloseVoipTask()
        {
            Debug.WriteLine($"{DateTime.Now} VoipTask closed.");
            Hub.Instance.VoipTaskInstance = null;
            _deferral?.Complete();
            _deferral = null;
        }
    }
}