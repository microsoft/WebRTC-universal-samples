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

#include <mfapi.h>
#include <mfidl.h>
#include <DXGI.h>
#include "Renderer.h"
#include "MediaEngineNotify.h"

using namespace ChatterBoxClient::Universal::BackgroundRenderer;
using namespace Platform;
using Microsoft::WRL::Wrappers::HStringReference;
using Microsoft::WRL::ComPtr;
using Microsoft::WRL::Details::Make;
using ABI::Windows::Foundation::Collections::IMap;
using ABI::Windows::Foundation::Collections::IPropertySet;

Renderer::Renderer() :
    _foregroundProcessId(0),
    _staleHandleTimestamp(0LL),
    _foregroundProcessIdChange(0),
    _newforegroundProcessId(0)
{
    InitializeCriticalSection(&_lock);
}

Renderer::~Renderer()
{
  OutputDebugString(L"Renderer::~Renderer()\n");
  Teardown();
  DeleteCriticalSection(&_lock);
}

void Renderer::Teardown() {
  OutputDebugString(L"Renderer::Teardown()\n");
  if (_dx11DeviceContext != nullptr)
  {
      // End pipeline
      _dx11DeviceContext->ClearState();
  }
  if (_device != nullptr)
  {
      ComPtr<IDXGIDevice3> dxDevice;
      if (SUCCEEDED(_device.As(&dxDevice)))
      {
          // Release all temporary buffers allocated for the app
          dxDevice->Trim();
      }
  }
  if (_mediaEngine != nullptr) {
    OutputDebugString(L"_mediaEngine->Shutdown()\n");
    _mediaEngine->Shutdown();
    _mediaEngine.Reset();
  }
  _mediaEngineEx.Reset();
  _mediaExtensionManager.Reset();
  _extensionManagerProperties.Reset();
  _device.Reset();
  _dx11DeviceContext.Reset();
  _swapChainHandle.Close();

  _streamSource = nullptr;
}

bool Renderer::IsInitialized::get() {
  return _mediaEngine != nullptr;
}

void Renderer::SetupRenderer(uint32 foregroundProcessId, Windows::Media::Core::IMediaSource^ streamSource,
    Windows::Foundation::Size videoControlSize)
{
    OutputDebugString(L"Renderer::SetupRenderer\n");
    _renderControlSize = videoControlSize;
    _streamSource = streamSource;
    _foregroundProcessId = foregroundProcessId;
    SetupSchemeHandler();
    SetupDirectX();
    boolean replaced;
    auto streamInspect = reinterpret_cast<IInspectable*>(streamSource);
    // Create a random URL that we'll use to map to the media source.
    std::wstring url(L"webrtc://");
    GUID result;
    HRESULT hr = CoCreateGuid(&result);
    if (FAILED(hr))
    {
      throw ref new COMException(hr, ref new String(L"Failed to create a GUID"));
    }
    Guid gd(result);
    url += gd.ToString()->Data();
    // Insert the url and the media source in the map.
    hr = _extensionManagerProperties->Insert(HStringReference(url.c_str()).Get(), streamInspect, &replaced);
    if (FAILED(hr))
    {
      throw ref new COMException(hr, ref new String(L"Failed to insert a media stream into media properties"));
    }
    // Set the source URL on the media engine.
    // The scheme handler will find the media source for the given URL and
    // return it to the media engine.
    BSTR sourceBSTR;
    sourceBSTR = SysAllocString(url.c_str());
    hr = _mediaEngine->SetSource(sourceBSTR);
    SysFreeString(sourceBSTR);
    if (FAILED(hr))
    {
      throw ref new COMException(hr, ref new String(L"Failed to set media source"));
    }
    // Finally, trigger a load on the media engine.
    hr = _mediaEngine->Load();
    if (FAILED(hr))
    {
      throw ref new COMException(hr, ref new String(L"Failed load media from source"));
    }
}

void Renderer::SetRenderControlSize(Windows::Foundation::Size size)
{
    EnterCriticalSection(&_lock);
    _renderControlSize = size;
    AsyncRecalculateScale();
    LeaveCriticalSection(&_lock);
}

void Renderer::UpdateForegroundProcessId(uint32 foregroundProcessId)
{
    EnterCriticalSection(&_lock);
    _newforegroundProcessId = foregroundProcessId;
    LeaveCriticalSection(&_lock);
    InterlockedIncrement(&_foregroundProcessIdChange);
}

uint32 Renderer::GetProcessId()
{
    return ::GetCurrentProcessId();
}

bool Renderer::GetRenderFormat(int64* swapChainHandle, uint32* width, uint32* height, uint32* foregroundProcessId)
{
    if (_swapChainHandle.GetRemoteHandle() == INVALID_HANDLE_VALUE ||
        swapChainHandle == nullptr || width == nullptr || height == nullptr || foregroundProcessId == nullptr)
    {
        return false;
    }

    *swapChainHandle = (int64)_swapChainHandle.GetRemoteHandle();
    
    DWORD w, h;
    _mediaEngine->GetNativeVideoSize(&w, &h);
    *width = w;
    *height = h;
    *foregroundProcessId = _foregroundProcessId;
    return true;
}

void Renderer::OnMediaEngineEvent(uint32 meEvent, uintptr_t param1, uint32 param2)
{
  HANDLE swapChainHandle;
  switch ((DWORD)meEvent)
  {
  case MF_MEDIA_ENGINE_EVENT_ERROR:
    // Throw media engine errors so we catch them in the debugger.
    throw ref new COMException((HRESULT)param2, ref new String(L"Failed OnMediaEngineEvent"));
    break;
  case MF_MEDIA_ENGINE_EVENT_FORMATCHANGE:
    // When the format changes, get a new swap chain handle and
    // send it to the foreground process.
    ReleaseStaleSwapChainHandleWhenExpired();
    CheckForegroundProcessId();
    if ((SUCCEEDED(_mediaEngineEx->GetVideoSwapchainHandle(&swapChainHandle))) &&
      (swapChainHandle != nullptr) && (swapChainHandle != INVALID_HANDLE_VALUE))
    {
      SendSwapChainHandle(swapChainHandle);
    }
    break;
  case MF_MEDIA_ENGINE_EVENT_CANPLAY:
    // Start playing automatically.
    _mediaEngine->Play();
    break;
  case MF_MEDIA_ENGINE_EVENT_TIMEUPDATE:
    // Various timed checks and cleanups.
    ReleaseStaleSwapChainHandleWhenExpired();
    CheckForegroundProcessId();
    break;
  }
}

void Renderer::SetupSchemeHandler()
{
  using Windows::Foundation::ActivateInstance;
  // Create a media extension manager.  It's used to register a scheme handler.
  HRESULT hr = ActivateInstance(HStringReference(RuntimeClass_Windows_Media_MediaExtensionManager).Get(), &_mediaExtensionManager);
  if (FAILED(hr))
  {
    throw ref new COMException(hr, ref new String(L"Failed to create media extension manager"));
  }
  // Create an IMap container.  It maps a source URL with an IMediaSource so it can be retrieved by the scheme handler.
  ComPtr<IMap<HSTRING, IInspectable*>> props;
  hr = ActivateInstance(HStringReference(RuntimeClass_Windows_Foundation_Collections_PropertySet).Get(), &props);
  if (FAILED(hr))
  {
    throw ref new COMException(hr, ref new String(L"Failed to create collection property set"));
  }
  // Register the scheme handler.  It takes the IMap container so it can be passed to the scheme
  // handler when its invoked with a given source URL.
  // The SchemeHandler will extract the IMediaSource from the map.
  ComPtr<IPropertySet> propSet;
  props.As(&propSet);
  HStringReference clsid(L"ChatterBoxClient.Universal.BackgroundRenderer.SchemeHandler");
  HStringReference scheme(L"webrtc:");
  hr = _mediaExtensionManager->RegisterSchemeHandlerWithSettings(clsid.Get(), scheme.Get(), propSet.Get());
  if (FAILED(hr))
  {
    throw ref new COMException(hr, ref new String(L"Failed to to register scheme handler"));
  }
  _extensionManagerProperties = props;
}

void Renderer::SetupDirectX()
{
    HRESULT hr = MFStartup(MF_VERSION);
    if (FAILED(hr))
    {
        throw ref new COMException(hr, ref new String(L"MFStartup failed"));
    }

    CreateDXDevice();
    UINT resetToken;
    // Create a device manager
    Microsoft::WRL::ComPtr<IMFDXGIDeviceManager> dxGIManager;
    hr = MFCreateDXGIDeviceManager(&resetToken, &dxGIManager);
    if (FAILED(hr))
    {
      throw ref new COMException(hr, ref new String(L"MFCreateDXGIDeviceManager failed"));
    }
    // Pass is the DirectX device created above.
    hr = dxGIManager->ResetDevice(_device.Get(), resetToken);
    if (FAILED(hr))
    {
      throw ref new COMException(hr, ref new String(L"ResetDevice failed"));
    }
    // These attributes will be passed to the media engine created below.
    ComPtr<IMFAttributes> attributes;
    hr = MFCreateAttributes(&attributes, 3);
    if (FAILED(hr))
    {
      throw ref new COMException(hr, ref new String(L"MFCreateAttributes failed"));
    }
    // Pass it the device manager which contains the DirectX device.
    hr = attributes->SetUnknown(MF_MEDIA_ENGINE_DXGI_MANAGER, (IUnknown*)dxGIManager.Get());
    if (FAILED(hr))
    {
      throw ref new COMException(hr, ref new String(L"Failed to set the DXGI manager"));
    }
    // Set a callback to receive media engine events.
    ComPtr<MediaEngineNotify> notify;
    notify = Make<MediaEngineNotify>();
    notify->SetCallback(this);
    hr = attributes->SetUnknown(MF_MEDIA_ENGINE_CALLBACK, (IUnknown*)notify.Get());
    if (FAILED(hr))
    {
      throw ref new COMException(hr, ref new String(L"attributes->SetUnknown(MF_MEDIA_ENGINE_CALLBACK, (IUnknown*)notify.Get()) failed"));
    }

    // Set output video format.
    hr = attributes->SetUINT32(MF_MEDIA_ENGINE_VIDEO_OUTPUT_FORMAT, DXGI_FORMAT_NV12);
    if (FAILED(hr))
    {
      throw ref new COMException(hr, ref new String(L"attributes->SetUINT32(MF_MEDIA_ENGINE_VIDEO_OUTPUT_FORMAT, DXGI_FORMAT_NV12) failed"));
    }
    // Create the media engine.
    ComPtr<IMFMediaEngineClassFactory> factory;
    hr = CoCreateInstance(CLSID_MFMediaEngineClassFactory, nullptr, CLSCTX_ALL, IID_PPV_ARGS(&factory));
    if (FAILED(hr))
    {
      throw ref new COMException(hr, ref new String(L"Failed to create media engine class factory"));
    }
    hr = factory->CreateInstance(
      MF_MEDIA_ENGINE_REAL_TIME_MODE | MF_MEDIA_ENGINE_WAITFORSTABLE_STATE,
      attributes.Get(), &_mediaEngine);
    if (FAILED(hr))
    {
      throw ref new COMException(hr, ref new String(L"Failed to create media engine"));
    }

    // Query the IMFMediaEngineEx interface.
    // It contains additional functions used throughout the code.
    hr = _mediaEngine.As(&_mediaEngineEx);
    if (FAILED(hr))
    {
      throw ref new COMException(hr, ref new String(L"Failed to create media engineex"));
    }

    // This call allows us to get a swap chain HANDLE to pass to the UI.
    hr = _mediaEngineEx->EnableWindowlessSwapchainMode(TRUE);
    if (FAILED(hr))
    {
      throw ref new COMException(hr, ref new String(L"Failed to enable Windowsless swapchain mode"));
    }
    _mediaEngineEx->SetRealTimeMode(TRUE);
    // Skylake video adapter has an issue with scaling. Using mirror mode for this device is a workaround.
    if (TestForSkylakeDisplayAdapter())
    {
      OutputDebugString(L"Skylake display adapter detected, switching to mirror mode\n");
      _mediaEngineEx->EnableHorizontalMirrorMode(TRUE);
    }
}

void Renderer::CreateDXDevice()
{
  static const D3D_FEATURE_LEVEL levels[] =
  {
    D3D_FEATURE_LEVEL_11_1,
    D3D_FEATURE_LEVEL_11_0,
    D3D_FEATURE_LEVEL_10_1,
    D3D_FEATURE_LEVEL_10_0,
    D3D_FEATURE_LEVEL_9_1,
    D3D_FEATURE_LEVEL_9_2,
    D3D_FEATURE_LEVEL_9_3
  };

  D3D_FEATURE_LEVEL featureLevel;
  HRESULT hr = S_OK;

  // First attempt to use hardware device.
  hr = D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr,
    D3D11_CREATE_DEVICE_VIDEO_SUPPORT,
    levels, ARRAYSIZE(levels), D3D11_SDK_VERSION, &_device, &featureLevel,
    &_dx11DeviceContext);

  if (SUCCEEDED(hr))
  {
    ComPtr<ID3D10Multithread> multithread;
    hr = _device.Get()->QueryInterface(IID_PPV_ARGS(&multithread));
    if (FAILED(hr))
    {
      throw ref new COMException(hr, ref new String(L"Failed to set device to multithreaded"));
    }
    multithread->SetMultithreadProtected(TRUE);
  }
  else // Fallback to software implementation
  {
    hr = D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_WARP, nullptr,
      D3D11_CREATE_DEVICE_VIDEO_SUPPORT, levels, ARRAYSIZE(levels),
      D3D11_SDK_VERSION, &_device, &featureLevel, &_dx11DeviceContext);
    if (FAILED(hr))
    {
      throw ref new COMException(hr, ref new String(L"Failed to create a DX device"));
    }
  }
}

void Renderer::SendSwapChainHandle(HANDLE swapChain)
{
  // Update the remote swap chain handle.
  if (swapChain != INVALID_HANDLE_VALUE)
  {
    _swapChainHandle.DetachMove(_staleSwapChainHandle);
    _staleHandleTimestamp = GetTickCount64();
    _swapChainHandle.AssignHandle(swapChain, _foregroundProcessId);
  }
  // Along with the handle, we also send the dimensions of the video.
  DWORD width;
  DWORD height;
  _mediaEngine->GetNativeVideoSize(&width, &height);

  if (_swapChainHandle.GetRemoteHandle() != INVALID_HANDLE_VALUE)
  {
    RenderFormatUpdate((int64)_swapChainHandle.GetRemoteHandle(), width, height, _foregroundProcessId);
  }
  // Save the video dimensions and recalculate the scaling/cropping.
  Windows::Foundation::Size size;
  size.Width = (float)width;
  size.Height = (float)height;
  EnterCriticalSection(&_lock);
  _videoSize = size;
  AsyncRecalculateScale();
  LeaveCriticalSection(&_lock);
}

void Renderer::AsyncRecalculateScale()
{
    EnterCriticalSection(&_lock);
    Windows::Foundation::Size renderControlSize = _renderControlSize;
    Windows::Foundation::Size videoSize = _videoSize;
    LeaveCriticalSection(&_lock);
    concurrency::create_async([this, renderControlSize, videoSize]
    {
        RecalculateScale(renderControlSize, videoSize);
    });
}

void Renderer::RecalculateScale(Windows::Foundation::Size renderControlSize,
    Windows::Foundation::Size videoSize)
{
    if ((renderControlSize.Width <= 0.0f) || (renderControlSize.Height <= 0.0f) ||
        (videoSize.Width <= 0.0f) || (videoSize.Height <= 0.0f))
    {
        return;
    }
    if (_mediaEngineEx == nullptr)
    {
        return;
    }

    double videoAspect = double(videoSize.Width) / double(videoSize.Height);
    double renderControlAspect = double(renderControlSize.Width) / double(renderControlSize.Height);
    // Scale to fill the swap chain panel.
    double scalingFactor = (videoAspect > renderControlAspect) ?
      (double(renderControlSize.Height) / double(videoSize.Height)) :
      (double(renderControlSize.Width) / double(videoSize.Width));
    if (scalingFactor == 0.0)
    {
      scalingFactor = 0.0001;
    }
    int scaledRenderControlWidth = int(double(renderControlSize.Width) / scalingFactor);
    int scaledRenderControlHeight = int(double(renderControlSize.Height) / scalingFactor);
    // Amount to crop from the width and height.
    int cropX = max((int)videoSize.Width - scaledRenderControlWidth, 0);
    int cropY = max((int)videoSize.Height - scaledRenderControlHeight, 0);
    // Convert crop to percentage and divide by 2 to share the crop equally
    // on both edges.  This centers the final image.
    float cropFrX = float((double(cropX) / double(videoSize.Width)) / 2.0);
    float cropFrY = float((double(cropY) / double(videoSize.Height)) / 2.0);
    // The final crop/scale rectangle with values between 0.0 and 1.0.
    MFVideoNormalizedRect rect = MFVideoNormalizedRect{ cropFrX, cropFrY, 1.0f - cropFrX, 1.0f - cropFrY };
    RECT r = { 0, 0, (LONG)renderControlSize.Width , (LONG)renderControlSize.Height };
    MFARGB borderColour = { 0, 0, 0, 0xFF };
    _mediaEngineEx->UpdateVideoStream(&rect, &r, &borderColour);
}

void Renderer::ReleaseStaleSwapChainHandle()
{
    _staleSwapChainHandle.Close();
    _staleHandleTimestamp = 0;
}

void Renderer::ReleaseStaleSwapChainHandleWhenExpired()
{
    if (!_staleSwapChainHandle.IsValid())
    {
        return;
    }
    ULONGLONG currentTime = GetTickCount64();
    if ((currentTime - _staleHandleTimestamp) >= StaleHandleTimeoutMS)
    {
        ReleaseStaleSwapChainHandle();
    }
}

void Renderer::CheckForegroundProcessId()
{
  if (_foregroundProcessIdChange > 0)
  {
    EnterCriticalSection(&_lock);
    uint32 newForegroundProcessId = _newforegroundProcessId;
    LeaveCriticalSection(&_lock);
    InterlockedDecrement(&_foregroundProcessIdChange);
    if (newForegroundProcessId == _foregroundProcessId)
    {
      return;
    }
    _foregroundProcessId = newForegroundProcessId;
    HANDLE swapChainHandle = _swapChainHandle.DetachLocalHandle();
    ReleaseStaleSwapChainHandle();
    _swapChainHandle.DetachMove(_staleSwapChainHandle);
    _staleHandleTimestamp = GetTickCount64();
    _swapChainHandle.AssignHandle(swapChainHandle, _foregroundProcessId);
    SendSwapChainHandle(INVALID_HANDLE_VALUE);
  }
}

bool Renderer::TestForSkylakeDisplayAdapter()
{
    ComPtr<IDXGIFactory> factory;
    if (FAILED(CreateDXGIFactory1(__uuidof(IDXGIFactory), (void**)factory.GetAddressOf())))
    {
        return false;
    }
    ComPtr<IDXGIAdapter> adapter;
    HRESULT hr = factory->EnumAdapters(0, adapter.GetAddressOf());
    if (FAILED(hr))
    {
        return false;
    }
    ComPtr<IDXGIAdapter2> adapter2;
    if (FAILED(hr = adapter.As(&adapter2)))
    {
        return false;
    }
    DXGI_ADAPTER_DESC2 desc2;
    if (FAILED(hr = adapter2->GetDesc2(&desc2)))
    {
        return false;
    }
    // Test for Skylake (Intel(R) HD Graphics 515), we detected the problem with those 3 Device ID, there might be more
    if ((desc2.VendorId == 0x8086) && ((desc2.DeviceId == 0x191E) || (desc2.DeviceId == 0x1916) || (desc2.DeviceId == 0x191B)))
    {
        return true;
    }
    return false;
}