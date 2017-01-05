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
using Windows.Networking;
using Windows.Networking.Connectivity;
using ChatterBox.Background.Settings;
using ChatterBox.MVVM;

namespace ChatterBox.ViewModels
{
    public class WelcomeViewModel : BindableBase
    {
        private string _domain;
        private bool _isCompleted;
        private string _name;

        public WelcomeViewModel()
        {
            CompleteCommand = new DelegateCommand(OnCompleteCommandExecute, CanCompleteCommandExecute);
            ShowSettings = new DelegateCommand(() => OnShowSettings?.Invoke());

            Domain = RegistrationSettings.Domain;
            Name = RegistrationSettings.Name;

            IsCompleted = ValidateStrings(Name, Domain);

            if (string.IsNullOrEmpty(Name))
            {
                Name =
                    NetworkInformation.GetHostNames().FirstOrDefault(h => h.Type == HostNameType.DomainName).DisplayName;
            }
        }

        public DelegateCommand CompleteCommand { get; }

        public string Domain
        {
            get { return _domain; }
            set
            {
                if (SetProperty(ref _domain, value))
                {
                    CompleteCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsCompleted
        {
            get { return _isCompleted; }
            set { SetProperty(ref _isCompleted, value); }
        }

        public string Name
        {
            get { return _name; }
            set
            {
                if (SetProperty(ref _name, value))
                {
                    CompleteCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public DelegateCommand ShowSettings { get; set; }

        public event Action OnCompleted;
        public event Action OnShowSettings;

        private bool CanCompleteCommandExecute()
        {
            return ValidateStrings(Name, Domain);
        }

        private void OnCompleteCommandExecute()
        {
            RegistrationSettings.Name = Name;
            RegistrationSettings.Domain = Domain;
            IsCompleted = true;
            OnCompleted?.Invoke();
        }

        private bool ValidateStrings(params string[] strings)
        {
            return strings != null &&
                   strings.All(@string => !string.IsNullOrWhiteSpace(@string) && !string.IsNullOrEmpty(@string));
        }
    }
}