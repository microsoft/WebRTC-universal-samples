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

using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using ChatterBox.ViewModels;

namespace ChatterBox.Views
{
    public sealed partial class ContactsView
    {
        public ContactsView()
        {
            InitializeComponent();
            DataContextChanged += ContactsView_DataContextChanged;
            var inputPane = InputPane.GetForCurrentView();
            inputPane.Showing += InputPane_Showing;
            inputPane.Hiding += InputPane_Hiding;
        }

        private void ContactsView_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            var viewModel = DataContext as ContactsViewModel;
            if (viewModel == null) return;
            viewModel.RingtoneElement = RingtoneElement;
        }

        private void InputPane_Hiding(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            if (Visibility != Visibility.Visible)
                return;

            if (MainGrid.RowDefinitions != null && MainGrid.RowDefinitions.Count >= 1)
            {
                MainGrid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
                MainGrid.InvalidateArrange();
            }
        }

        private void InputPane_Showing(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            if (Visibility != Visibility.Visible)
                return;

            var coveredArea = sender.OccludedRect;
            var value = MainGrid.ActualHeight - coveredArea.Height;

            if (MainGrid.RowDefinitions != null &&
                MainGrid.RowDefinitions.Count >= 1 &&
                coveredArea.Height > 0 &&
                value > 0)
            {
                MainGrid.RowDefinitions[0].Height = new GridLength(value);
                MainGrid.InvalidateArrange();
            }
            args.EnsuredFocusedElementInView = true;
        }
    }
}