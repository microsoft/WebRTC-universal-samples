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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace PeerConnectionClient.Ortc.Utilities
{
    internal class AutoLock : IDisposable
    {
        private readonly SemaphoreSlim _sem;
        private bool _isLocked;

        public AutoLock(SemaphoreSlim sem)
        {
            _sem = sem;
        }

        public Task WaitAsync()
        {
            if (_isLocked) return Task.Run(() => { });
            _isLocked = true;
            var result = _sem.WaitAsync();
            return result;
        }

        public void Dispose()
        {
            if (_isLocked)
            {
                _sem.Release();
            }
        }
    }
}