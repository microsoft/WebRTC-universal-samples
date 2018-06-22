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

using PeerConnectionClient.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
#if UNITY
using UnityPlayer;
#endif

namespace PeerConnectionClient
{
    /// <summary>
    /// The application main page.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private MainViewModel _mainViewModel;

#if UNITY
        private WinRTBridge.WinRTBridge _bridge;

        private SplashScreen splash;
        private Rect splashImageRect;
        private WindowSizeChangedEventHandler onResizeHandler;
        private bool isPhone = false;
#endif

        /// <summary>
        /// Constructor.
        /// </summary>
        public MainPage()
        {
            this.InitializeComponent();
            NavigationCacheMode = Windows.UI.Xaml.Navigation.NavigationCacheMode.Required;

#if UNITY
            AppCallbacks appCallbacks = AppCallbacks.Instance;
            // Setup scripting bridge
            _bridge = new WinRTBridge.WinRTBridge();
            appCallbacks.SetBridge(_bridge);

            bool isWindowsHolographic = false;

#if UNITY_HOLOGRAPHIC
            // If application was exported as Holographic check if the deviceFamily actually supports it,
            // otherwise we treat this as a normal XAML application
            string deviceFamily = Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamily;
            isWindowsHolographic = String.Compare("Windows.Holographic", deviceFamily) == 0;
#endif

            if (isWindowsHolographic)
            {
                appCallbacks.InitializeViewManager(Window.Current.CoreWindow);
            }
            else
            {
                appCallbacks.RenderingStarted += () => { RemoveSplashScreen(); };

#if UNITY_UWP
                if (Windows.Foundation.Metadata.ApiInformation.IsApiContractPresent("Windows.Phone.PhoneContract", 1))
                    isPhone = true;
#endif
                appCallbacks.SetSwapChainPanel(GetSwapChainPanel());
                appCallbacks.SetCoreWindowEvents(Window.Current.CoreWindow);
                appCallbacks.InitializeD3DXAML();

                //splash = ((App)App.Current).splashScreen;
                GetSplashBackgroundColor();
                OnResize();
                onResizeHandler = new WindowSizeChangedEventHandler((o, e) => OnResize());
                Window.Current.SizeChanged += onResizeHandler;
            }
            PeerVideo.Visibility = Visibility.Collapsed;
            SelfVideo.Visibility = Visibility.Collapsed;
#else
            DXSwapChainPanel.Visibility = Visibility.Collapsed;
#endif // UNITY
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.  The Parameter
        /// property is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            _mainViewModel = (MainViewModel)e.Parameter;
            this.DataContext = _mainViewModel;
#if UNITY
            //splash = (SplashScreen)e.Parameter;
            OnResize();
#else
            _mainViewModel.PeerVideo = PeerVideo;
            _mainViewModel.SelfVideo = SelfVideo;
#endif

        }

#if UNITY
        private void OnResize()
        {
            if (splash != null)
            {
                splashImageRect = splash.ImageLocation;
                PositionImage();
            }
        }

        private void PositionImage()
        {
            var inverseScaleX = 1.0f;
            var inverseScaleY = 1.0f;
            if (isPhone)
            {
                inverseScaleX = inverseScaleX / DXSwapChainPanel.CompositionScaleX;
                inverseScaleY = inverseScaleY / DXSwapChainPanel.CompositionScaleY;
            }

            //ExtendedSplashImage.SetValue(Canvas.LeftProperty, splashImageRect.X * inverseScaleX);
            //ExtendedSplashImage.SetValue(Canvas.TopProperty, splashImageRect.Y * inverseScaleY);
            //ExtendedSplashImage.Height = splashImageRect.Height * inverseScaleY;
            //ExtendedSplashImage.Width = splashImageRect.Width * inverseScaleX;
        }

        private async void GetSplashBackgroundColor()
        {
            try
            {
                StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///AppxManifest.xml"));
                string manifest = await FileIO.ReadTextAsync(file);
                int idx = manifest.IndexOf("SplashScreen");
                manifest = manifest.Substring(idx);
                idx = manifest.IndexOf("BackgroundColor");
                if (idx < 0)  // background is optional
                    return;
                manifest = manifest.Substring(idx);
                idx = manifest.IndexOf("\"");
                manifest = manifest.Substring(idx + 1);
                idx = manifest.IndexOf("\"");
                manifest = manifest.Substring(0, idx);
                int value = 0;
                bool transparent = false;
                if (manifest.Equals("transparent"))
                    transparent = true;
                else if (manifest[0] == '#') // color value starts with #
                    value = Convert.ToInt32(manifest.Substring(1), 16) & 0x00FFFFFF;
                else
                    return; // at this point the value is 'red', 'blue' or similar, Unity does not set such, so it's up to user to fix here as well
                byte r = (byte)(value >> 16);
                byte g = (byte)((value & 0x0000FF00) >> 8);
                byte b = (byte)(value & 0x000000FF);

                await CoreWindow.GetForCurrentThread().Dispatcher.RunAsync(CoreDispatcherPriority.High, delegate ()
                {
                    byte a = (byte)(transparent ? 0x00 : 0xFF);
                    //ExtendedSplashGrid.Background = new SolidColorBrush(Color.FromArgb(a, r, g, b));
                });
            }
            catch (Exception)
            { }
        }

        public SwapChainPanel GetSwapChainPanel()
        {
            return DXSwapChainPanel;
        }

        public void RemoveSplashScreen()
        {
            //DXSwapChainPanel.Children.Remove(ExtendedSplashGrid);
            //if (onResizeHandler != null)
            //{
            //    Window.Current.SizeChanged -= onResizeHandler;
            //    onResizeHandler = null;
            //}
        }
#endif

        /// <summary>
        /// Invoked when the Add button is clicked 
        /// </summary>
        /// <param name="sender">The object where the handler is attached.</param>
        /// <param name="e">Details about the exception routed event.</param>
        private void ConfirmAddButton_Click(object sender, RoutedEventArgs e)
        {
            this.AddButton.Flyout.Hide();
        }

        /// <summary>
        /// Media Failed event handler for remote/peer video.
        /// Invoked when an error occurs in peer media source.
        /// </summary>
        /// <param name="sender">The object where the handler is attached.</param>
        /// <param name="e">Details about the exception routed event.</param>
        private void PeerVideo_MediaFailed(object sender, Windows.UI.Xaml.ExceptionRoutedEventArgs e)
        {
            if(_mainViewModel!=null)
            {
                _mainViewModel.PeerVideo_MediaFailed(sender, e);
            }
        }

        /// <summary>
        /// Media Failed event handler for self video.
        /// Invoked when an error occurs in self media source.
        /// </summary>
        /// <param name="sender">The object where the handler is attached.</param>
        /// <param name="e">Details about the exception routed event.</param>
        private void SelfVideo_MediaFailed(object sender, Windows.UI.Xaml.ExceptionRoutedEventArgs e)
        {
            if (_mainViewModel != null)
            {
                _mainViewModel.SelfVideo_MediaFailed(sender, e);
            }
        }
    }
}
