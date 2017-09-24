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
    /// The application main page.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private MainViewModel _mainViewModel;

        /// <summary>
        /// Constructor.
        /// </summary>
        public MainPage()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// See Page.OnNavigatedTo()
        /// </summary>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            _mainViewModel = (MainViewModel)e.Parameter;
            this.DataContext = _mainViewModel;
            _mainViewModel.PeerVideo = PeerVideo;
            _mainViewModel.SelfVideo = SelfVideo;
        }

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
