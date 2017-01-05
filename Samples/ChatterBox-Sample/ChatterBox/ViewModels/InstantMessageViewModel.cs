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
using Windows.UI.Xaml.Media;
using ChatterBox.Controls;
using ChatterBox.MVVM;

namespace ChatterBox.ViewModels
{
    public class InstantMessageViewModel : BindableBase, IInstantMessage
    {
        private bool _isHighlighted;
        public string Body { get; set; }
        public DateTime DeliveredAt { get; set; }

        public bool IsHighlighted
        {
            get { return _isHighlighted; }
            set { SetProperty(ref _isHighlighted, value); }
        }

        public bool IsSender { get; set; }
        public string SenderName { get; set; }
        public ImageSource SenderProfileSource { get; set; }
    }
}