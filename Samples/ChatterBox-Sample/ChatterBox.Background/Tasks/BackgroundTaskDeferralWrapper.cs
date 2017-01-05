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
using Windows.ApplicationModel.Background;

namespace ChatterBox.Background.Tasks
{
    public sealed class BackgroundTaskDeferralWrapper : IDisposable
    {
        private BackgroundTaskDeferral _deferral;

        public BackgroundTaskDeferralWrapper(BackgroundTaskDeferral deferral)
        {
            _deferral = deferral;
        }


        public void Dispose()
        {
            _deferral.Complete();
            _deferral = null;
        }
    }
}