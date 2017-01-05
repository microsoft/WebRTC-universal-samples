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

#include "MediaEngineNotify.h"

using namespace ChatterBoxClient::Universal::BackgroundRenderer;

MediaEngineNotify::~MediaEngineNotify()
{
}

void MediaEngineNotify::SetCallback(MediaEngineNotifyCallback^ callback)
{
  _callback = callback;
}

IFACEMETHODIMP MediaEngineNotify::EventNotify(DWORD evt, DWORD_PTR param1, DWORD param2)
{
  if (evt == MF_MEDIA_ENGINE_EVENT_NOTIFYSTABLESTATE)
  {
    SetEvent(reinterpret_cast<HANDLE>(param1));
  }
  else
  {
    if (_callback != nullptr) {
      _callback->OnMediaEngineEvent((unsigned int)evt, param1, param2);
    }
  }
  return S_OK;
}