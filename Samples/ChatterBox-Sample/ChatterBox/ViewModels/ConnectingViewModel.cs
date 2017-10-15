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
using System.Threading.Tasks;
using ChatterBox.Background.Signalling;
using ChatterBox.Background.Signalling.PersistedData;
using ChatterBox.MVVM;
using ChatterBox.Services;

namespace ChatterBox.ViewModels
{
    public class ConnectingViewModel : BindableBase
    {
        private readonly ISocketConnection _connection;
        private bool _isConnecting;
        private string _status;

        public ConnectingViewModel(IForegroundUpdateService foregroundUpdateService,
            ISocketConnection socketConnection)
        {
            _connection = socketConnection;

            foregroundUpdateService.OnRegistrationStatusUpdated += OnRegistrationStatusUpdated;

            ConnectCommand = new DelegateCommand(OnConnectCommandExecute, OnConnectCommandCanExecute);
            ShowSettings = new DelegateCommand(() => OnShowSettings?.Invoke());

            Task.Run(UpdateStatusAsync).Wait();
        }

        public DelegateCommand ConnectCommand { get; }

        public DelegateCommand ShowSettings { get; set; }

        public string Status
        {
            get { return _status; }
            set { SetProperty(ref _status, value); }
        }

        public async Task EstablishConnection()
        {
            var isConnected = await _connection.GetIsConnectedAsync();
            if (isConnected)
            {
                if (SignallingStatus.IsRegistered)
                {
                    OnRegistered?.Invoke();
                }
                else
                {
                    await _connection.RegisterAsync();
                }
            }
            else
            {
                _isConnecting = true;
                await UpdateStatusAsync();

                await _connection.ConnectAsync();

                _isConnecting = false;
                await UpdateStatusAsync();
            }
        }

        public event Action OnRegistered;
        public event Action OnRegistrationFailed;
        public event Action OnShowSettings;

        public async Task SwitchSignallingServer()
        {
            var isConnected = await _connection.GetIsConnectedAsync();
            if (isConnected)
            {
                await _connection.DisconnectAsync();
                await EstablishConnection();
            }
        }

        private bool OnConnectCommandCanExecute()
        {
            var connectionStatusTask = _connection.GetIsConnectedAsync().AsTask();
            connectionStatusTask.Wait();
            var isConnected = connectionStatusTask.Result;
            var ret = !isConnected && !_isConnecting;
            return ret;
        }

        private async void OnConnectCommandExecute()
        {
            await EstablishConnection();
        }

        private void OnRegistrationStatusUpdated()
        {
            if (SignallingStatus.IsRegistered)
            {
                OnRegistered?.Invoke();
            }
            else
            {
                OnRegistrationFailed?.Invoke();
                Status = "Connection to the server has been terminated.";
                ConnectCommand.RaiseCanExecuteChanged();
            }
        }

        private async Task UpdateStatusAsync()
        {
            var isConnected = await _connection.GetIsConnectedAsync();
            if (!isConnected && !_isConnecting)
            {
                Status = "Failed to connect to the server. Check your settings and try again.";
            }
            else if (!_isConnecting)
            {
                Status = "Registering with the server...";
            }

            ConnectCommand.RaiseCanExecuteChanged();
        }
    }
}