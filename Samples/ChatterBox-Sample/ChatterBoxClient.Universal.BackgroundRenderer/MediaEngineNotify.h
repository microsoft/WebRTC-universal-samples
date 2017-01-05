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

#include <Mfmediaengine.h>
#include "MediaEngineNotifyCallback.h"
#include <wrl.h>
#include <wrl\client.h>
#include <wrl\implements.h>
#include <wrl\ftm.h>
#include <wrl\event.h>
#include <wrl\wrappers\corewrappers.h>
#include <wrl\module.h>

namespace ChatterBoxClient { namespace Universal { namespace BackgroundRenderer {
            
class MediaEngineNotify :
    public Microsoft::WRL::RuntimeClass<
    Microsoft::WRL::RuntimeClassFlags<Microsoft::WRL::RuntimeClassType::ClassicCom>,
    IMFMediaEngineNotify>
{
public:
    // MediaEngineNotify
    void SetCallback(MediaEngineNotifyCallback^ callback);
    // IMFMediaEngineNotify
    IFACEMETHOD(EventNotify)(DWORD evt, DWORD_PTR param1, DWORD param2);
private:
    virtual ~MediaEngineNotify();
    MediaEngineNotifyCallback^ _callback;
};

}}}
