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
using Windows.UI.Xaml;
using ChatterBox.MVVM;

namespace ChatterBox.Services
{
    public sealed class LayoutService : BindableBase
    {
        private Window _layoutRoot;
        private LayoutType _layoutType;

        static LayoutService()
        {
            Instance = new LayoutService();
        }

        public static LayoutService Instance { get; private set; }

        public Window LayoutRoot
        {
            get { return _layoutRoot; }
            set
            {
                if (_layoutRoot != null)
                {
                    _layoutRoot.SizeChanged -= LayoutRootSizeChanged;
                }
                _layoutRoot = value;
                _layoutRoot.SizeChanged += LayoutRootSizeChanged;
                UpdateLayout(_layoutRoot.Bounds.Height, _layoutRoot.Bounds.Width);
            }
        }

        public LayoutType LayoutType
        {
            get { return _layoutType; }
            set
            {
                if (SetProperty(ref _layoutType, value))
                {
                    LayoutChanged?.Invoke(value);
                }
            }
        }

        public event Action<LayoutType> LayoutChanged;

        public void UpdateLayout(double height, double width)
        {
            if (ApplicationView.GetForCurrentView().Orientation == ApplicationViewOrientation.Landscape)
            {
                LayoutType = width > 800 ? LayoutType.Parallel : LayoutType.Overlay;
            }
            else
            {
                LayoutType = LayoutType.Overlay;
            }
        }

        private void LayoutRootSizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            UpdateLayout(e.Size.Height, e.Size.Width);
        }
    }
}