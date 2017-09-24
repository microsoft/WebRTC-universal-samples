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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace PeerConnectionClient
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        /// <summary>
        /// Allows tracking page views, exceptions and other telemetry through the Microsoft Application Insights service.
        /// </summary>
        // Temporarily disable.  Problems loading Microsoft.Diagnostics.Tracing.EventSource
        //public TelemetryClient TelemetryClient = new TelemetryClient();

        ViewModels.MainViewModel mainViewModel;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {

#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                this.DebugSettings.EnableFrameRateCounter = true;
            }
#endif

            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();
                // Set the default language
                rootFrame.Language = Windows.Globalization.ApplicationLanguages.Languages[0];

                rootFrame.NavigationFailed += OnNavigationFailed;

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            //Do not activate now.
            //https://msdn.microsoft.com/en-us/library/windows/apps/hh465338.aspx:
            //"Flicker occurs if you activate the current window (by calling Window.Current.Activate)
            //before the content of the page finishes rendering. You can reduce the likelihood of seeing
            //a flicker by making sure your extended splash screen image has been read before you activate
            //the current window. Additionally, you should use a timer to try to avoid the flicker by
            //making your application wait briefly, 50ms for example, before you activate the current window.
            //Unfortunately, there is no guaranteed way to prevent the flicker because XAML renders content
            //asynchronously and there is no guaranteed way to predict when rendering will be complete."
            mainViewModel = new ViewModels.MainViewModel(CoreApplication.MainView.CoreWindow.Dispatcher);
            mainViewModel.OnInitialized += OnMainViewModelInitialized;
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            // Perform suspending logic on non UI thread to avoid deadlocks
            // since some ongoing flows may need access to UI thread
            new System.Threading.Tasks.Task(async () =>
            {
                await mainViewModel.OnAppSuspending();
            deferral.Complete();
            }).Start();
        }

        /// <summary>
        /// Invoked when the application MainViewModel is initialized.
        /// Creates the application initial page
        /// </summary>
        private void OnMainViewModelInitialized()
        {
            Frame rootFrame = Window.Current.Content as Frame;
            if (!rootFrame.Navigate(typeof(MainPage), mainViewModel))
            {
                throw new Exception("Failed to create initial page");
            }
            Window.Current.Activate();
        }
    }
}
