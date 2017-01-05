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

namespace ChatterBox.Converters
{
    public sealed class NullToVisibilityConverter : IValueConverter
    {
        public bool Inverted { get; set; }


        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var booleanValue = value != null;

            return booleanValue
                ? (!Inverted ? Visibility.Visible : Visibility.Collapsed)
                : (!Inverted ? Visibility.Collapsed : Visibility.Visible);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}