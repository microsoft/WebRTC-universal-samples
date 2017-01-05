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

namespace ChatterBox.Services
{
    public class FrameFormatEventArgs : EventArgs
    {
        public readonly uint Height;
        public readonly long SwapChainHandle;
        public readonly uint Width;

        public FrameFormatEventArgs(long swapChainHandle, uint width, uint height)
        {
            SwapChainHandle = swapChainHandle;
            Width = width;
            Height = height;
        }
    }
}