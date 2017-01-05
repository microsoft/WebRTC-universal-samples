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
using ChatterBox.Background.Settings;
using ChatterBox.MVVM;

namespace ChatterBox.ViewModels
{
    public class IceServerViewModel : BindableBase
    {
        private bool _isSelected;

        private string _password;

        private string _url;

        private string _username;

        public IceServerViewModel(IceServer iceServer)
        {
            if (iceServer == null)
                throw new ArgumentNullException();
            IceServer = iceServer;

            Url = iceServer.Url;
            Username = iceServer.Username;
            Password = iceServer.Password;
        }

        public IceServer IceServer { get; }

        public bool IsSelected
        {
            get { return _isSelected; }
            set { SetProperty(ref _isSelected, value); }
        }

        public string Password
        {
            get { return _password; }
            set { SetProperty(ref _password, value); }
        }

        public string Url
        {
            get { return _url; }
            set { SetProperty(ref _url, value); }
        }

        public string Username
        {
            get { return _username; }
            set { SetProperty(ref _username, value); }
        }

        public bool Apply()
        {
            IceServer.Url = Url;
            IceServer.Username = Username;
            IceServer.Password = Password;

            return !string.IsNullOrEmpty(Url);
        }
    }
}