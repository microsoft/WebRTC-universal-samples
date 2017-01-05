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
using Windows.Foundation;
using Windows.Media.Core;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

namespace ChatterBox.Background.Call
{
    public interface IVideoRenderHelper : IDisposable
    {
        IAsyncAction InitializeAsync(uint foregroundProcessId, IMediaSource source,
            Size videoControlSize);

        bool IsInitialized();
        event RenderFormatUpdateHandler RenderFormatUpdate;
        void SetDisplaySize(Size size);

        void UpdateForegroundProcessId(uint foregroundProcessId);
        bool GetRenderFormat(out Int64 swapChainHandle, out UInt32 width, out UInt32 height, out UInt32 foregroundProcessId);
    }
}