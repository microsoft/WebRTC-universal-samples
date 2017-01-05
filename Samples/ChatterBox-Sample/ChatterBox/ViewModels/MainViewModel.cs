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
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using ChatterBox.Background.Signaling.PersistedData;
using ChatterBox.MVVM;

namespace ChatterBox.ViewModels
{
    public sealed class MainViewModel : DispatcherBindableBase
    {
        private bool _isActive;
        private bool _isSettingsVisible;

        public MainViewModel(
            WelcomeViewModel welcomeViewModel,
            ConnectingViewModel connectingViewModel,
            ContactsViewModel contactsViewModel,
            SettingsViewModel settingsViewModel,
            CoreDispatcher uiDispatcher) : base(uiDispatcher)
        {
            if (welcomeViewModel == null || connectingViewModel == null ||
                contactsViewModel == null || settingsViewModel == null ||
                uiDispatcher == null)
            {
                throw new ArgumentNullException();
            }
            WelcomeViewModel = welcomeViewModel;
            ConnectingViewModel = connectingViewModel;
            ContactsViewModel = contactsViewModel;
            SettingsViewModel = settingsViewModel;

            WelcomeViewModel.OnCompleted += WelcomeCompleted;
            ConnectingViewModel.OnRegistered += ConnectingViewModel_OnRegistered;
            ConnectingViewModel.OnRegistrationFailed += ConnectingViewModel_OnRegistrationFailed;
            ShowSettingsCommand = new DelegateCommand(() => IsSettingsVisible = true);

            WelcomeViewModel.OnShowSettings += () => IsSettingsVisible = true;
            ContactsViewModel.OnShowSettings += () => IsSettingsVisible = true;
            ConnectingViewModel.OnShowSettings += () => IsSettingsVisible = true;
            SettingsViewModel.OnClose += SettingsViewModelOnClose;
            SettingsViewModel.OnRegistrationSettingsChanged += RegistrationSettingChanged;
        }

        public ConnectingViewModel ConnectingViewModel { get; }
        public ContactsViewModel ContactsViewModel { get; }

        public bool IsActive
        {
            get { return _isActive; }
            set { SetProperty(ref _isActive, value); }
        }

        public bool IsSettingsVisible
        {
            get { return _isSettingsVisible; }
            set
            {
                SetProperty(ref _isSettingsVisible, value);
                if (value) SettingsViewModel.OnNavigatedTo();
                else SettingsViewModel.OnNavigatedFrom();
            }
        }

        public SettingsViewModel SettingsViewModel { get; }
        public DelegateCommand ShowSettingsCommand { get; set; }
        public WelcomeViewModel WelcomeViewModel { get; }

        public void OnNavigatedTo()
        {
            if (WelcomeViewModel.IsCompleted) WelcomeCompleted();
            if (SignalingStatus.IsRegistered)
            {
                //_signalingProxy.GetPeerList(new Message());
            }
        }

        private void ConnectingViewModel_OnRegistered()
        {
            IsActive = true;
        }

        private void ConnectingViewModel_OnRegistrationFailed()
        {
            IsActive = false;
        }

        private async void RegistrationSettingChanged()
        {
            IsActive = false;
            await ConnectingViewModel.SwitchSignalingServer();
        }

        private void SettingsViewModelOnClose()
        {
            IsSettingsVisible = false;
        }

        private async void WelcomeCompleted()
        {
            ApplicationView.GetForCurrentView().Title = WelcomeViewModel.Name;
            await ConnectingViewModel.EstablishConnection();
        }
    }
}