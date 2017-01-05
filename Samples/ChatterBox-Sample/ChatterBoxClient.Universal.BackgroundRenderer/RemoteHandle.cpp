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

#include "RemoteHandle.h"
#include <exception>

RemoteHandle::RemoteHandle() :
    _localHandle(INVALID_HANDLE_VALUE),
    _remoteHandle(INVALID_HANDLE_VALUE),
    _processId(0),
    _processHandle(INVALID_HANDLE_VALUE)
{
}


RemoteHandle::~RemoteHandle()
{
    Close();
    if (_processHandle != INVALID_HANDLE_VALUE)
    {
        CloseHandle(_processHandle);
        _processHandle = INVALID_HANDLE_VALUE;
    }
}

RemoteHandle& RemoteHandle::AssignHandle(HANDLE localHandle, DWORD processId)
{
    if (localHandle != _localHandle)
    {
        HANDLE remoteHandle = INVALID_HANDLE_VALUE;
        HANDLE processHandle = _processHandle;
        bool newProcessHandle = false;
        if ((processId != _processId) || (_processHandle == INVALID_HANDLE_VALUE))
        {
            newProcessHandle = true;
            processHandle = OpenProcess(PROCESS_DUP_HANDLE, TRUE, processId);
            if ((processHandle == nullptr) || (processHandle == INVALID_HANDLE_VALUE))
            {
                processHandle = INVALID_HANDLE_VALUE;
            }
        }
        if ((processHandle != INVALID_HANDLE_VALUE) &&
            (!DuplicateHandle(GetCurrentProcess(),
                localHandle, processHandle, &remoteHandle, 0, TRUE, DUPLICATE_SAME_ACCESS)))
        {
            remoteHandle = INVALID_HANDLE_VALUE;
        }
        Close();
        if (newProcessHandle)
        {
            if (_processHandle != INVALID_HANDLE_VALUE)
            {
                CloseHandle(_processHandle);
            }
            _processHandle = processHandle;
            _processId = processId;
        }
        _localHandle = localHandle;
        _remoteHandle = remoteHandle;
    }
    return *this;
}

RemoteHandle& RemoteHandle::Close()
{
    if (_localHandle != INVALID_HANDLE_VALUE)
    {
        CloseHandle(_localHandle);
        _localHandle = INVALID_HANDLE_VALUE;
    }
    if (_remoteHandle != INVALID_HANDLE_VALUE)
    {
        DuplicateHandle(_processHandle, _remoteHandle, nullptr, nullptr, 0, TRUE, DUPLICATE_CLOSE_SOURCE);
        _remoteHandle = INVALID_HANDLE_VALUE;
    }
    return *this;
}

HANDLE RemoteHandle::GetLocalHandle() const
{
    return _localHandle;
}
HANDLE RemoteHandle::GetRemoteHandle() const
{
    return _remoteHandle;
}

RemoteHandle& RemoteHandle::DetachMove(RemoteHandle& destRemoteHandle)
{
    if (&destRemoteHandle == this)
    {
        return *this;
    }
    destRemoteHandle.Close();
    if (destRemoteHandle._processHandle != INVALID_HANDLE_VALUE)
    {
        CloseHandle(destRemoteHandle._processHandle);
    }
    destRemoteHandle._localHandle = _localHandle;
    destRemoteHandle._remoteHandle = _remoteHandle;
    destRemoteHandle._processId = _processId;
    destRemoteHandle._processHandle = _processHandle;
    _localHandle = INVALID_HANDLE_VALUE;
    _remoteHandle = INVALID_HANDLE_VALUE;
    _processId = 0;
    _processHandle = INVALID_HANDLE_VALUE;
    return *this;
}

HANDLE RemoteHandle::DetachLocalHandle()
{
    HANDLE handle = _localHandle;
    _localHandle = INVALID_HANDLE_VALUE;
    return handle;
}

bool RemoteHandle::IsValid() const
{
    return ((_localHandle != INVALID_HANDLE_VALUE) &&
        (_remoteHandle != INVALID_HANDLE_VALUE));
}
