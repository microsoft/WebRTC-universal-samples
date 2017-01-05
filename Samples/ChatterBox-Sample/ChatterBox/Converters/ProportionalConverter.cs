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
using Windows.UI.Xaml.Data;

namespace ChatterBox.Converters
{
    public class ProportionalConverter : IValueConverter
    {
        /// <summary>
        ///     Represented in percents - %. (e.g. 25)
        /// </summary>
        public double DownscalingFactor { get; set; } = 25;


        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var refDimension = (double) value;
            var scaledDimension = DownscalingFactor/100*refDimension;
            return scaledDimension;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}