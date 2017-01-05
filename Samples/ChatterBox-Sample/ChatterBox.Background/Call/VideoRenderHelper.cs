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
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using ChatterBoxClient.Universal.BackgroundRenderer;

namespace ChatterBox.Background.Call
{
    public sealed class VideoRenderHelper : IVideoRenderHelper
    {
        private readonly Renderer _renderer = new Renderer();
        private bool _isSetup;

        public VideoRenderHelper()
        {
            _renderer.RenderFormatUpdate += OnRenderFormatUpdate;
        }

        public void Dispose()
        {
            _renderer.RenderFormatUpdate -= OnRenderFormatUpdate;
            _renderer.Teardown();
        }

        public IAsyncAction InitializeAsync(uint foregroundProcessId, IMediaSource source,
            Size videoControlSize)
        {
            return Task.Run(() =>
            {
                _renderer.SetupRenderer(foregroundProcessId, source, videoControlSize);
                _isSetup = true;
            }).AsAsyncAction();
        }

        public bool IsInitialized()
        {
            return _isSetup;
        }

        public event RenderFormatUpdateHandler RenderFormatUpdate;

        private void OnRenderFormatUpdate(long swapChainHandle, uint width, uint height, uint foregroundProcessId)
        {
            RenderFormatUpdate(swapChainHandle, width, height, foregroundProcessId);
        }

        public bool GetRenderFormat(out Int64 swapChainHandle, out UInt32 width, out UInt32 height, out UInt32 foregroundProcessId)
        {
            return _renderer.GetRenderFormat(out swapChainHandle, out width, out height, out foregroundProcessId);
        }

        public void SetDisplaySize(Size size)
        {
            _renderer.SetRenderControlSize(size);
        }

        public void UpdateForegroundProcessId(uint foregroundProcessId)
        {
            _renderer.UpdateForegroundProcessId(foregroundProcessId);
        }
    }
}