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
using Windows.UI;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace ChatterBox.Converters
{
    public sealed class BoolToBrushConverter : IValueConverter
    {
        public Color ColorForFalse { get; set; }
        public Color ColorForTrue { get; set; }


        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var boolValue = (bool) value;
            var color = boolValue
                ? ColorForTrue
                : ColorForFalse;
            return new SolidColorBrush(color);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}