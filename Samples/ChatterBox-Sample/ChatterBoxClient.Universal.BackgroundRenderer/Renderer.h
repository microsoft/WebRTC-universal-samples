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

#pragma once
#include "MediaEngineNotifyCallback.h"
#include "RemoteHandle.h"
#include <collection.h>
#include <ppltasks.h>
#include <d3d11_2.h>
#include <d2d1_2.h>
#include <mfidl.h>
#include <mfapi.h>
#include <mferror.h>

#include <windows.foundation.h>
#include <windows.foundation.collections.h>
#include <windows.media.h>
#include <windows.media.capture.h>
#include <windows.media.mediaproperties.h>
#include <Mfmediaengine.h>
#include <wrl\wrappers\corewrappers.h>

namespace ChatterBoxClient { namespace Universal { namespace BackgroundRenderer {

public delegate void RenderFormatUpdateHandler(int64 swapChainHandle, uint32 width, uint32 height, uint32 foregroundProcessId);

[Windows::Foundation::Metadata::WebHostHidden]
public ref class Renderer sealed :
    public MediaEngineNotifyCallback
{
public:
    Renderer();
    void Teardown();
    virtual ~Renderer();

    /// Initializes the renderer by creating an IMFMediaEngine,
    /// setup all DirectX flags and attaches the IMediaSource
    /// to the IMFMediaEngine.
    /// Following this call, RenderFormatUpdate will be triggered
    /// with a swap chain HANDLE to be passed on to the UI process.
    void SetupRenderer(
      uint32 foregroundProcessId,
      Windows::Media::Core::IMediaSource^ streamSource,
      Windows::Foundation::Size videoControlSize);

    /// Notifies of a change in render control size.
    /// This will trigger a recalculation of the scaling/cropping.
    /// Should be called whenever the swap chain panel is resized.
    void SetRenderControlSize(Windows::Foundation::Size size);

    void UpdateForegroundProcessId(uint32 foregroundProcessId);
    static uint32 GetProcessId();

    property bool IsInitialized
    {
      bool get();
    }

    bool GetRenderFormat(int64* swapChainHandle, uint32* width, uint32* height, uint32* foregroundProcessId);
    event RenderFormatUpdateHandler^ RenderFormatUpdate;

    // Implement MediaEngineNotifyCallback
    virtual void OnMediaEngineEvent(uint32 meEvent, uintptr_t param1, uint32 param2);
private:
    void SetupSchemeHandler();
    void SetupDirectX();
    void CreateDXDevice();
    void SendSwapChainHandle(HANDLE swapChain);
    void AsyncRecalculateScale();
    void RecalculateScale(Windows::Foundation::Size renderControlSize,
        Windows::Foundation::Size videoSize);
    void ReleaseStaleSwapChainHandle();
    void ReleaseStaleSwapChainHandleWhenExpired();
    void CheckForegroundProcessId();
    static bool TestForSkylakeDisplayAdapter();

    Microsoft::WRL::ComPtr<ABI::Windows::Media::IMediaExtensionManager> _mediaExtensionManager;
    Microsoft::WRL::ComPtr<ABI::Windows::Foundation::Collections::IMap<HSTRING, IInspectable*>> _extensionManagerProperties;
    Microsoft::WRL::ComPtr<ID3D11Device> _device;
    Microsoft::WRL::ComPtr<ID3D11DeviceContext> _dx11DeviceContext;
    Microsoft::WRL::ComPtr<IMFMediaEngine> _mediaEngine;
    Microsoft::WRL::ComPtr<IMFMediaEngineEx> _mediaEngineEx;
    DWORD _foregroundProcessId;
    RemoteHandle _swapChainHandle;
    RemoteHandle _staleSwapChainHandle;
    ULONGLONG _staleHandleTimestamp;
    Windows::Media::Core::IMediaSource^ _streamSource;
    Windows::Foundation::Size _renderControlSize;
    Windows::Foundation::Size _videoSize;
    CRITICAL_SECTION _lock;
    LONG _foregroundProcessIdChange;
    uint32 _newforegroundProcessId;

    static const ULONGLONG StaleHandleTimeoutMS = 2000LL;
};

}}}
