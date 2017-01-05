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
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using ChatterBox.Background.AppService;
using ChatterBox.Background.AppService.Dto;
using ChatterBox.Background.Avatars;
using ChatterBox.Background.Signaling.PersistedData;
using ChatterBox.MVVM;
using ChatterBox.Services;

namespace ChatterBox.ViewModels
{
    public sealed class ContactsViewModel : DispatcherBindableBase
    {
        private readonly ICallChannel _callChannel;
        private readonly Func<ConversationViewModel> _contactFactory;
        private CallState _currentCallState;
        private ConversationViewModel _selectedConversation;
        private bool _showAsParallel;

        public ContactsViewModel(IForegroundUpdateService foregroundUpdateService, ICallChannel callChannel,
            Func<ConversationViewModel> contactFactory, CoreDispatcher uiDispatcher) : base(uiDispatcher)
        {
            _contactFactory = contactFactory;
            _callChannel = callChannel;
            foregroundUpdateService.OnPeerDataUpdated += OnPeerDataUpdated;
            foregroundUpdateService.GetShownUser += ForegroundUpdateService_GetShownUser;
            foregroundUpdateService.OnCallStatusUpdate += OnCallStatusUpdate;
            OnPeerDataUpdated();

            LayoutService.Instance.LayoutChanged += LayoutChanged;
            LayoutChanged(LayoutService.Instance.LayoutType);
            ShowSettings = new DelegateCommand(() => OnShowSettings?.Invoke());
        }

        public ObservableCollection<ConversationViewModel> Conversations { get; } =
            new ObservableCollection<ConversationViewModel>();

        public MediaElement RingtoneElement { get; set; }

        public ConversationViewModel SelectedConversation
        {
            get { return _selectedConversation; }
            set
            {
                _selectedConversation?.OnNavigatedFrom();
                SetProperty(ref _selectedConversation, value);
                _selectedConversation?.OnNavigatedTo();
            }
        }

        public bool ShowAsParallel
        {
            get { return _showAsParallel; }
            set { SetProperty(ref _showAsParallel, value); }
        }

        public DelegateCommand ShowSettings { get; set; }

        public async Task InitializeAsync()
        {
            var callStatus = await _callChannel.GetCallStatusAsync();
            if (callStatus != null)
            {
                OnCallStatusUpdate(callStatus);
            }
            OnPeerDataUpdated();
        }

        public event Action OnShowSettings;


        public async Task PlayRingtone(bool isIncomingCall)
        {
            if (RingtoneElement == null) return;
            var source = isIncomingCall
                ? "ms-appx:///Assets/Ringtones/IncomingCall.mp3"
                : "ms-appx:///Assets/Ringtones/OutgoingCall.mp3";

            await RingtoneElement.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                RingtoneElement.Source = new Uri(source);
                RingtoneElement.Stop();
                RingtoneElement.Play();
            });
        }

        public bool SelectConversation(string userId)
        {
            foreach (var conversation in Conversations)
            {
                if (conversation.UserId == userId)
                {
                    SelectedConversation = conversation;
                    return true;
                }
            }
            return false;
        }


        public async Task StopSound()
        {
            if (RingtoneElement == null) return;

            await RingtoneElement.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                RingtoneElement.Stop();
                RingtoneElement.Source = null;
            });
        }

        private void Contact_OnCloseConversation(ConversationViewModel obj)
        {
            SelectedConversation = null;
        }

        private void Contact_OnIsInCallMode(ConversationViewModel conversation)
        {
            SelectedConversation = conversation;
        }


        private string ForegroundUpdateService_GetShownUser()
        {
            if (SelectedConversation != null)
            {
                return SelectedConversation.UserId;
            }
            return string.Empty;
        }

        private void LayoutChanged(LayoutType layout)
        {
            if (_currentCallState == CallState.Idle)
            {
                ShowAsParallel = layout == LayoutType.Parallel;
            }
            else
            {
                ShowAsParallel = false;
            }
            UpdateSelection();
        }

        private async void OnCallStatusUpdate(CallStatus callStatus)
        {
            await RunOnUiThread(async () =>
            {
                _currentCallState = callStatus.State;

                switch (callStatus.State)
                {
                    case CallState.LocalRinging:
                        await PlayRingtone(true);
                        ShowAsParallel = false;
                        UpdateSelection();
                        break;
                    case CallState.RemoteRinging:
                        await PlayRingtone(false);
                        ShowAsParallel = false;
                        UpdateSelection();
                        break;
                    case CallState.Idle:
                        await StopSound();
                        ShowAsParallel = LayoutService.Instance.LayoutType == LayoutType.Parallel;
                        UpdateSelection();
                        break;
                    case CallState.EstablishOutgoing:
                    case CallState.EstablishIncoming:
                        await StopSound();
                        break;
                    case CallState.HangingUp:
                    case CallState.ActiveCall:
                    case CallState.Held:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            });
        }

        private async void OnPeerDataUpdated()
        {
            var peers = SignaledPeerData.Peers;
            var copyConversations = new ObservableCollection<ConversationViewModel>(Conversations);
            foreach (var conversation in copyConversations)
            {
                if (peers.All(p => p.UserId != conversation.UserId))
                {
                    Conversations.Remove(conversation);
                    conversation.Dispose();
                }
            }
            foreach (var peer in peers)
            {
                var contact = Conversations.SingleOrDefault(s => s.UserId == peer.UserId);
                if (contact == null)
                {
                    contact = _contactFactory();
                    contact.Name = peer.Name;
                    contact.UserId = peer.UserId;
                    contact.ProfileSource = new BitmapImage(new Uri(AvatarLink.EmbeddedLinkFor(peer.Avatar)));
                    contact.OnCloseConversation += Contact_OnCloseConversation;
                    contact.OnIsInCallMode += Contact_OnIsInCallMode;
                    var sortList = Conversations.ToList();
                    sortList.Add(contact);
                    sortList = sortList.OrderBy(s => s.Name).ToList();
                    Conversations.Insert(sortList.IndexOf(contact), contact);
                    await contact.InitializeAsync();
                }
                contact.IsOnline = peer.IsOnline;
            }
            UpdateSelection();
        }

        private void UpdateSelection()
        {
            if (SelectedConversation == null && LayoutService.Instance.LayoutType == LayoutType.Parallel)
            {
                SelectedConversation = Conversations.FirstOrDefault();
            }
        }
    }
}