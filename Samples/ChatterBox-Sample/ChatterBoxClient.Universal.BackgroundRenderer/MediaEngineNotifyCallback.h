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
#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

namespace ChatterBoxClient { namespace Universal { namespace BackgroundRenderer {

public interface struct MediaEngineNotifyCallback
{
    void OnMediaEngineEvent(uint32 meEvent, uintptr_t param1, uint32 param2) = 0;
};

}}}
