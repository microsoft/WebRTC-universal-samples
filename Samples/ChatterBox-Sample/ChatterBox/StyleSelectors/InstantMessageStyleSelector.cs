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

using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using ChatterBox.ViewModels;

namespace ChatterBox.StyleSelectors
{
    public class InstantMessageStyleSelector : StyleSelector
    {
        public Style OwnMessageStyle { get; set; }
        public Style PeerMessageStyle { get; set; }

        protected override Style SelectStyleCore(object item, DependencyObject container)
        {
            var message = (InstantMessageViewModel) item;
            return message.IsSender ? OwnMessageStyle : PeerMessageStyle;
        }
    }
}