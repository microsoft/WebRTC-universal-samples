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

using Windows.System;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;

namespace ChatterBox.Behaviors
{
    public static class ReturnKeyCloseKeyboardBehavior
    {
        public static readonly DependencyProperty EnabledProperty =
            DependencyProperty.RegisterAttached("Enabled", typeof (bool), typeof (ReturnKeyCloseKeyboardBehavior),
                new PropertyMetadata(0, OnEnabledPropertyChanged));

        public static bool GetEnabled(DependencyObject obj)
        {
            return (bool) obj.GetValue(EnabledProperty);
        }

        public static void SetEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(EnabledProperty, value);
        }

        private static void Element_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            var dp = sender as DependencyObject;
            if (!GetEnabled(dp)) return;
            if (e.Key == VirtualKey.Enter)
            {
                InputPane.GetForCurrentView().TryHide();
            }
        }

        private static void OnEnabledPropertyChanged(DependencyObject dp, DependencyPropertyChangedEventArgs e)
        {
            var element = dp as UIElement;

            if (element == null) return;

            if (e.OldValue != null)
            {
                element.KeyDown -= Element_KeyDown;
            }

            if (e.NewValue != null)
            {
                element.KeyDown += Element_KeyDown;
            }
        }
    }
}