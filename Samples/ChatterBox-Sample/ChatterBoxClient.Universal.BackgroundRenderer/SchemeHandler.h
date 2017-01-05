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

#include <wrl.h>
#include <wrl\client.h>
#include <wrl\implements.h>
#include <wrl\ftm.h>
#include <wrl\event.h>
#include <wrl\wrappers\corewrappers.h>
#include <wrl\module.h>
#include <windows.media.h>
#include <mfidl.h>
#include <mfapi.h>

namespace ChatterBoxClient { namespace Universal { namespace BackgroundRenderer {

class DECLSPEC_UUID("E2CFE911-260A-4169-90E1-B51AD2B08711") SchemeHandler :
    public Microsoft::WRL::RuntimeClass<
    Microsoft::WRL::RuntimeClassFlags< Microsoft::WRL::RuntimeClassType::WinRtClassicComMix>,
    ABI::Windows::Media::IMediaExtension,
    IMFSchemeHandler>
{
    InspectableClass(L"ChatterBoxClient.Universal.BackgroundRenderer.SchemeHandler", BaseTrust)
public:
    SchemeHandler();
    ~SchemeHandler();
    // IMediaExtension
    IFACEMETHOD(SetProperties) (ABI::Windows::Foundation::Collections::IPropertySet *pConfiguration);

    // IMFSchemeHandler
    IFACEMETHOD(BeginCreateObject) (
        _In_ LPCWSTR pwszURL,
        _In_ DWORD dwFlags,
        _In_ IPropertyStore *pProps,
        _COM_Outptr_opt_  IUnknown **ppIUnknownCancelCookie,
        _In_ IMFAsyncCallback *pCallback,
        _In_ IUnknown *punkState);

    IFACEMETHOD(EndCreateObject) (
        _In_ IMFAsyncResult *pResult,
        _Out_  MF_OBJECT_TYPE *pObjectType,
        _Out_  IUnknown **ppObject);

    IFACEMETHOD(CancelObjectCreation) (
        _In_ IUnknown *pIUnknownCancelCookie);

private:
    Microsoft::WRL::ComPtr<ABI::Windows::Foundation::Collections::IPropertySet> _extensionManagerProperties;
};
CoCreatableClass(SchemeHandler);

ActivatableClass(SchemeHandler)

}}}
