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

class RemoteHandle
{
public:
    RemoteHandle();
    ~RemoteHandle();
    RemoteHandle& AssignHandle(HANDLE localHandle, DWORD processId);
    RemoteHandle& Close();
    HANDLE GetLocalHandle() const;
    HANDLE GetRemoteHandle() const;
    RemoteHandle& DetachMove(RemoteHandle& destRemoteHandle);
    HANDLE DetachLocalHandle();
    bool IsValid() const;
private:
    RemoteHandle(const RemoteHandle&);
    const RemoteHandle& operator = (const RemoteHandle&) { return *this;  };
    HANDLE _localHandle;
    HANDLE _remoteHandle;
    DWORD _processId;
    HANDLE _processHandle;
};

