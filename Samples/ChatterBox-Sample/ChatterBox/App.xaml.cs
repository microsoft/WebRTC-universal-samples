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
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.Core;
using Windows.Graphics.Display;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using ChatterBox.Background;
using ChatterBox.Background.AppService;
using ChatterBox.Background.AppService.Dto;
using ChatterBox.Background.Avatars;
using ChatterBox.Background.Notifications;
using ChatterBox.Background.Settings;
using ChatterBox.Background.Signaling;
using ChatterBox.Background.Signaling.PersistedData;
using ChatterBox.Background.Tasks;
using ChatterBox.Client.WebRTCSwapChainPanel;
using ChatterBox.Communication.Contracts;
using ChatterBox.Services;
using ChatterBox.ViewModels;
using ChatterBox.Views;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Practices.Unity;
using CallState = ChatterBox.Controls.CallState;

namespace ChatterBox
{
    /// <summary>
    ///     Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public sealed partial class App
    {
        /// <summary>
        ///     Initializes the singleton application object.  This is the first line of authored code
        ///     executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            WindowsAppInitializer.InitializeAsync(
                WindowsCollectors.Metadata |
                WindowsCollectors.PageView |
                WindowsCollectors.Session);
            TelemetryConfiguration.Active.DisableTelemetry =
                !SignalingSettings.AppInsightsEnabled;
            UnhandledException += CurrentDomain_UnhandledException;
            InitializeComponent();
            Suspending += OnSuspending;
            Resuming += OnResuming;
        }

        public UnityContainer Container { get; } = new UnityContainer();

        public async Task ShowDialog(string message)
        {
            var messageDialog = new MessageDialog(message);
            await messageDialog.ShowAsync();
        }

        /// <summary>
        ///     Invoked when the application is launched normally by the end user.  Other entry points
        ///     will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            ToastNotificationLaunchArguments launchArg = null;

            if (!string.IsNullOrEmpty(e.Arguments))
            {
                launchArg = ToastNotificationLaunchArguments.FromXmlString(e.Arguments);
            }
            if (e.PreviousExecutionState == ApplicationExecutionState.Running ||
                e.PreviousExecutionState == ApplicationExecutionState.Suspended)
            {
                ProcessLaunchArgument(launchArg);
                return;
            }

            await AvatarLink.ExpandAvatarsToLocal();

            //Register IoC types
            if (!Container.IsRegistered<HubClient>())
            {
                Container.RegisterInstance(CoreApplication.MainView.CoreWindow.Dispatcher);
                Container.RegisterType<TaskHelper>(new ContainerControlledLifetimeManager());
                Container.RegisterType<HubClient>(new ContainerControlledLifetimeManager());
                Container.RegisterInstance<IForegroundUpdateService>(Container.Resolve<HubClient>(),
                    new ContainerControlledLifetimeManager());
                Container.RegisterInstance<IForegroundChannel>(Container.Resolve<HubClient>(),
                    new ContainerControlledLifetimeManager());
                Container.RegisterInstance<ISignalingSocketChannel>(Container.Resolve<HubClient>(),
                    new ContainerControlledLifetimeManager());
                Container.RegisterInstance<IClientChannel>(Container.Resolve<HubClient>(),
                    new ContainerControlledLifetimeManager());
                Container.RegisterInstance<ICallChannel>(Container.Resolve<HubClient>(),
                    new ContainerControlledLifetimeManager());
                Container.RegisterInstance<IMediaSettingsChannel>(Container.Resolve<HubClient>(),
                    new ContainerControlledLifetimeManager());
                Container.RegisterType<ISocketConnection, SocketConnection>(new ContainerControlledLifetimeManager());
                Container.RegisterType<NtpService>(new ContainerControlledLifetimeManager());
                Container.RegisterType<MainViewModel>(new ContainerControlledLifetimeManager());
                Container.RegisterType<SettingsViewModel>(new ContainerControlledLifetimeManager());
            }

            Container.Resolve<HubClient>().OnDisconnectedFromHub -= App_OnDisconnectedFromHub;
            Container.Resolve<HubClient>().OnDisconnectedFromHub += App_OnDisconnectedFromHub;
            Container.Resolve<SettingsViewModel>().OnQuitApp -= QuitApp;
            Container.Resolve<SettingsViewModel>().OnQuitApp += QuitApp;

            await ConnectHubClient();

            await RegisterForPush(Container.Resolve<TaskHelper>());

            var signalingTask = await RegisterSignalingTask(Container.Resolve<TaskHelper>(), false);
            if (signalingTask == null)
            {
                var message = new MessageDialog("The signaling task is required.");
                await message.ShowAsync();
                return;
            }

            await RegisterSessionConnectedTask(Container.Resolve<TaskHelper>());

            var rootFrame = Window.Current.Content as Frame;

            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
                // When the navigation stack isn't restored navigate to the first page,
                // configuring the new page by passing required information as a navigation
                // parameter
                rootFrame.Navigate(typeof (MainView), Container.Resolve<MainViewModel>());
            }

            if (e.PreviousExecutionState == ApplicationExecutionState.ClosedByUser)
            {
                await Resume();
            }

            ProcessLaunchArgument(launchArg);

            // Ensure the current window is active
            Window.Current.Activate();
        }

        protected override void OnWindowCreated(WindowCreatedEventArgs args)
        {
            LayoutService.Instance.LayoutRoot = args.Window;
            base.OnWindowCreated(args);
        }

        private async void App_OnDisconnectedFromHub()
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal, async () => await ConnectHubClient());
        }

        private async Task ConnectHubClient()
        {
            if (!Container.Resolve<HubClient>().IsConnected)
            {
                var client = Container.Resolve<HubClient>();

                // Make this call blocking, since we don't want try sending message until the hub is connected (especially on a resume)
                var connected = Task.Run(client.Connect).Result;
                if (connected)
                {
                    await client.SetForegroundProcessIdAsync(
                        WebRTCSwapChainPanel.CurrentProcessId);

                    await RegisterForDisplayOrientationChangeAsync();
                }
                else
                {
                    await ShowDialog("Failed to connect to the Hub!");
                }
            }
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (!SignalingSettings.AppInsightsEnabled) return;
            var excTelemetry = new ExceptionTelemetry(e.Exception)
            {
                SeverityLevel = SeverityLevel.Critical,
                HandledAt = ExceptionHandledAt.Unhandled,
                Timestamp = DateTimeOffset.UtcNow
            };
            var telemetry = new TelemetryClient();
            telemetry.TrackException(excTelemetry);
            telemetry.Flush();
        }

        /// <summary>
        ///     Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        private async void OnOrientationChanged(DisplayInformation sender, object args)
        {
            var client = Container.Resolve<HubClient>();
            await client.DisplayOrientationChangedAsync(sender.CurrentOrientation);
        }

        private async void OnResuming(object sender, object e)
        {
            Debug.WriteLine("App.OnResuming");

            ETWEventLogger.Instance.LogEvent("Application Resuming", DateTimeOffset.Now.ToUnixTimeMilliseconds());

            if (SignalingSettings.AppInsightsEnabled)
            {
                var telemetry = new TelemetryClient();
                var eventTel = new EventTelemetry("Application Resuming") {Timestamp = DateTimeOffset.UtcNow};
                telemetry.TrackEvent(eventTel);
            }
            await Resume();
        }

        /// <summary>
        ///     Invoked when application execution is being suspended.  Application state is saved
        ///     without knowing whether the application will be terminated or resumed with the contents
        ///     of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private async void OnSuspending(object sender, SuspendingEventArgs e)
        {
            Debug.WriteLine($"{DateTime.Now.ToString("HH:mm:ss.ffff")} App.OnSuspending");
            ETWEventLogger.Instance.LogEvent("Application Suspending", DateTimeOffset.Now.ToUnixTimeMilliseconds());
            var deferral = e.SuspendingOperation.GetDeferral();

            var client = Container.Resolve<HubClient>();

            // Suspend video capture and rendering in the background.
            await client.SuspendCallVideoAsync();

            if (SignalingSettings.AppInsightsEnabled)
            {
                var telemetry = new TelemetryClient();
                var eventTel = new EventTelemetry("Application Suspending") {Timestamp = DateTimeOffset.UtcNow};
                telemetry.TrackEvent(eventTel);
            }
            
            // Disconnect the rendering on the UI.
            // We do it here instead of waiting for the background
            // to notify us because we're about to be suspended.
            await client.OnUpdateFrameFormatAsync(new FrameFormat
            {
                IsLocal = true,
                Width = 0,
                Height = 0,
                SwapChainHandle = 0,
                ForegroundProcessId = 0
            });
            await client.OnUpdateFrameFormatAsync(new FrameFormat
            {
                IsLocal = false,
                Width = 0,
                Height = 0,
                SwapChainHandle = 0,
                ForegroundProcessId = 0
            });

            // Hangup pending calls
            var callStatus = await client.GetCallStatusAsync();
            if (callStatus != null &&
                (callStatus.State == Background.AppService.Dto.CallState.LocalRinging ||
                 callStatus.State == Background.AppService.Dto.CallState.RemoteRinging))
            {
                await client.HangupAsync();
            }

            Debug.WriteLine($"{DateTime.Now.ToString("HH:mm:ss.ffff")} App.OnSuspending - done");
            deferral.Complete();
        }

        private void ProcessLaunchArgument(ToastNotificationLaunchArguments launchArg)
        {
            if (launchArg == null) return;
            switch (launchArg.Type)
            {
                case NotificationType.InstantMessage:
                    Container.Resolve<MainViewModel>().ContactsViewModel.SelectConversation(
                        (string) launchArg.Arguments[ArgumentType.FromId]);
                    break;
            }
        }

        private async void QuitApp()
        {
            await Container.Resolve<ICallChannel>().HangupAsync();
            UnRegisterAllBackgroundTask();
            Current.Exit();
        }

        private async Task RegisterForDisplayOrientationChangeAsync()
        {
            var displayInfo = DisplayInformation.GetForCurrentView();
            displayInfo.OrientationChanged -= OnOrientationChanged;
            displayInfo.OrientationChanged += OnOrientationChanged;

            var client = Container.Resolve<HubClient>();
            await client.DisplayOrientationChangedAsync(displayInfo.CurrentOrientation);
        }

        private static async Task RegisterForPush(TaskHelper helper, bool registerAgain = true)
        {
            try
            {
                PushNotificationHelper.RegisterPushNotificationChannel();

                var pushNotificationTask =
                    await helper.RegisterTask(nameof(PushNotificationTask), typeof (PushNotificationTask).FullName,
                        new PushNotificationTrigger(), registerAgain).AsTask();
                if (pushNotificationTask == null)
                {
                    Debug.WriteLine("Push notification background task is not started");
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Failed to register for push notification. Error: {e.Message}");
            }
        }

        private static async Task<IBackgroundTaskRegistration> RegisterSessionConnectedTask(TaskHelper helper)
        {
            var sessionConnTask = helper.GetTask(nameof(SessionConnectedTask)) ??
                                  await helper.RegisterTask(nameof(SessionConnectedTask),
                                      typeof (SessionConnectedTask).FullName,
                                      new SystemTrigger(SystemTriggerType.InternetAvailable, false), false);
            return sessionConnTask;
        }

        private static async Task<IBackgroundTaskRegistration> RegisterSignalingTask(TaskHelper helper,
            bool registerAgain = true)
        {
            var signalingTask = helper.GetTask(nameof(SignalingTask)) ??
                                await helper.RegisterTask(nameof(SignalingTask), typeof (SignalingTask).FullName,
                                    new SocketActivityTrigger(), registerAgain).AsTask();

            return signalingTask;
        }

        private async Task Resume()
        {
            // Reconnect the Hub client
            await ConnectHubClient();

            // Reconnect the Signaling socket
            if (Container.IsRegistered(typeof (ISocketConnection)))
            {
                if (Container.Resolve<HubClient>().IsConnected)
                {
                    var socketConnection = Container.Resolve<ISocketConnection>();
                    var isConnected = await socketConnection.GetIsConnectedAsync();
                    if (!isConnected) await socketConnection.ConnectAsync();
                }
            }

            // If network condition changed during the app was suspended, the foreground might miss some events
            // from the background, let's trigger an early heartbeat to detect it.
            await Container.Resolve<IClientChannel>().ClientHeartBeatAsync();
            // After sending client heartbeat and we are not registered, then, show the connecting view
            // with updated registration status
            var foregroundUpdateChannel = Container.Resolve<IForegroundChannel>();
            if (!SignalingStatus.IsRegistered)
            {
                Container.Resolve<MainViewModel>().IsActive = false;
            }
            await foregroundUpdateChannel.OnSignaledRegistrationStatusUpdatedAsync();

            var contactView = Container.Resolve<MainViewModel>().ContactsViewModel;
            await contactView.InitializeAsync();
            /* If the call was hung-up while we were suspended, we need to update the UI */
            if (contactView.SelectedConversation != null)
            {
                if (contactView.SelectedConversation.CallState != CallState.Idle)
                {
                    // By calling Initialize, we force to get the call state from the background
                    await contactView.SelectedConversation.InitializeAsync();
                }
            }

            // Force reload all stored relay messages
            await foregroundUpdateChannel.OnSignaledRelayMessagesUpdatedAsync();

            var client = Container.Resolve<HubClient>();
            if (client.IsConnected)
            {
                await client.SetForegroundProcessIdAsync(WebRTCSwapChainPanel.CurrentProcessId);
                await client.ResumeCallVideoAsync();
            }

            Window.Current.Activate();
        }

        private void UnRegisterAllBackgroundTask()
        {
            var helper = new TaskHelper();
            var signalingReg = helper.GetTask(nameof(SignalingTask));
            signalingReg?.Unregister(true);

            var regOp = helper.GetTask(nameof(PushNotificationTask));
            regOp?.Unregister(true);
        }
    }
}
