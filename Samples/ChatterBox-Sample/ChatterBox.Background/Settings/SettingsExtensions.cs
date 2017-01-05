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

namespace ChatterBox.Background.Settings
{
    public static class SettingsExtensions
    {
        public static void AddOrUpdate(this IPropertySet propertySet, string key, object value)
        {
            if (propertySet.ContainsKey(key))
            {
                propertySet[key] = value;
            }
            else
            {
                propertySet.Add(key, value);
            }
        }
    }
}