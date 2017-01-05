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
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;

namespace ChatterBox.Background.Tasks
{
    public sealed class ForegroundAppServiceTask : IBackgroundTask
    {
        private BackgroundTaskDeferral _deferral;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            try
            {
                var triggerDetail = (AppServiceTriggerDetails)taskInstance.TriggerDetails;
                // Keep a deferral to prevent the task from terminating.
                _deferral = taskInstance.GetDeferral();

                // Save this connection in the hub.  It will be used for bidirectional communication.
                Hub.Instance.ForegroundConnection = triggerDetail.AppServiceConnection;
                Hub.Instance.ForegroundTask = this;

                taskInstance.Canceled += (s, e) => Close();
                triggerDetail.AppServiceConnection.ServiceClosed += (s, e) => Close();
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


        private void Close()
        {
            Hub.Instance.ForegroundTask = null;
            Hub.Instance.ForegroundConnection = null;
            _deferral?.Complete();
        }
    }
}