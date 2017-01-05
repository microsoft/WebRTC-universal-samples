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
using Windows.Foundation;
using Windows.UI.Core;

namespace ChatterBox.MVVM
{
    /// <summary>
    ///     Provides ability to run the UI updates in UI thread.
    /// </summary>
    public abstract class DispatcherBindableBase : BindableBase
    {
        // The UI dispatcher
        protected readonly CoreDispatcher UiDispatcher;

        /// <summary>
        ///     Creates a DispatcherBindableBase instance.
        /// </summary>
        /// <param name="uiDispatcher">Core event message dispatcher.</param>
        protected DispatcherBindableBase(CoreDispatcher uiDispatcher)
        {
            UiDispatcher = uiDispatcher;
        }

        /// <summary>
        ///     Overrides the BindableBase's OnPropertyChanged method.
        /// </summary>
        /// <param name="propertyName">The name of the changed property.</param>
        protected override void OnPropertyChanged(string propertyName)
        {
            RunOnUiThread(() => base.OnPropertyChanged(propertyName)).AsTask();
        }

        /// <summary>
        ///     Schedules the provided callback on the UI thread from a worker thread
        /// </summary>
        /// <param name="fn">The function to execute</param>
        protected IAsyncAction RunOnUiThread(Action fn)
        {
            return UiDispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(fn));
        }
    }
}