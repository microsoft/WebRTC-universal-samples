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


using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace ChatterBox.Behaviors
{
    public static class ListBoxAutoScrollBehavior
    {
        public static readonly DependencyProperty EnabledProperty =
            DependencyProperty.RegisterAttached("Enabled", typeof (bool), typeof (ListBoxAutoScrollBehavior),
                new PropertyMetadata(0, OnEnabledPropertyChanged));

        public static bool GetEnabled(DependencyObject obj)
        {
            return (bool) obj.GetValue(EnabledProperty);
        }

        public static void SetEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(EnabledProperty, value);
        }

        private static void OnEnabledPropertyChanged(DependencyObject dp, DependencyPropertyChangedEventArgs e)
        {
            var element = dp as ListBox;
            if (element == null)
                return;
            var newValue = (bool) e.NewValue;

            var autoScrollWorker = new VectorChangedEventHandler<object>((s1, e2) => ScrollToLastItem(element));
            if (element.Items == null) return;

            if (newValue)
            {
                element.Items.VectorChanged += autoScrollWorker;
            }
            else
            {
                element.Items.VectorChanged -= autoScrollWorker;
            }
        }

        private static void ScrollToLastItem(ListBox listBox)
        {
            if (listBox.Items != null && listBox.Items.Count > 0)
            {
                var lastItem = listBox.Items[listBox.Items.Count - 1];
                listBox.UpdateLayout();
                listBox.ScrollIntoView(lastItem);
            }
        }
    }
}