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
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Foundation;

namespace ChatterBox.Background.Tasks
{
    public sealed class TaskHelper
    {
        public IBackgroundTaskRegistration GetTask(string name)
        {
            return BackgroundTaskRegistration.AllTasks
                .Select(s => s.Value)
                .FirstOrDefault(s => s.Name == name);
        }

        public IAsyncOperation<IBackgroundTaskRegistration> RegisterTask(string name, string entrypoint,
            IBackgroundTrigger trigger, bool removeIfRegistered)
        {
            return RegisterTaskP(
                name,
                entrypoint,
                trigger,
                removeIfRegistered).AsAsyncOperation();
        }

        private async Task<IBackgroundTaskRegistration> RegisterTaskP(string name, string entrypoint,
            IBackgroundTrigger trigger, bool removeIfRegistered)
        {
            var taskRegistration = GetTask(name);
            if (taskRegistration != null)
            {
                if (removeIfRegistered)
                {
                    taskRegistration.Unregister(true);
                }
                else
                {
                    return taskRegistration;
                }
            }
            await BackgroundExecutionManager.RequestAccessAsync();
            var taskBuilder = new BackgroundTaskBuilder
            {
                Name = name,
                TaskEntryPoint = entrypoint
            };
            taskBuilder.SetTrigger(trigger);
            return taskBuilder.Register();
        }
    }
}