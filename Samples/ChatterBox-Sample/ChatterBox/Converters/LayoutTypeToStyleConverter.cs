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
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using ChatterBox.Services;

namespace ChatterBox.Converters
{
    public class LayoutTypeToStyleConverter : IValueConverter
    {
        public Style OverlayStyle { get; set; }
        public Style ParallelStyle { get; set; }


        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var newState = (LayoutType) value;
            switch (newState)
            {
                case LayoutType.Parallel:
                    return ParallelStyle;
                case LayoutType.Overlay:
                    return OverlayStyle;
                default:
                    return OverlayStyle;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}