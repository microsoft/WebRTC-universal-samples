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

// The Windows APIs used in this file will fail WACK tests.
// shall remove _APP_PERFORMANCE_ from the "conditional compiling symbol" in the project setting
// e.g.: ChatterBox.Background-->properties-->build
// in the final build
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ChatterBox.Background.Call.Utils
{
#if _APP_PERFORMANCE_
    internal static class MEMData
    {
        // WINDOWS_PHONE_APP is not available for vs2015 when build win10 for arm.
        // Check USE_WIN10_PHONE_DLL (defined by us) until vs2015 fix this.
#if WINDOWS_PHONE_APP || USE_WIN10_PHONE_DLL
        [DllImport("api-ms-win-core-sysinfo-l1-2-0.dll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = true)]
        private static extern IntPtr GetCurrentProcess();
#else
        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetCurrentProcess();
#endif
#if WINDOWS_PHONE_APP || USE_WIN10_PHONE_DLL
        [DllImport("api-ms-win-core-sysinfo-l1-2-0.dll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = true)]
        private static extern bool GetProcessMemoryInfo(IntPtr hProcess, out PROCESS_MEMORY_COUNTERS_EX counters, uint size);
#else
        [DllImport("psapi.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool GetProcessMemoryInfo(IntPtr hProcess, out PROCESS_MEMORY_COUNTERS_EX counters,
            uint size);
#endif


        /// <summary>
        ///     Get the current memory usage
        /// </summary>
        public static long GetMEMUsage()
        {
            long ret = 0;
            PROCESS_MEMORY_COUNTERS_EX memoryCounters;

            memoryCounters.cb = (uint) Marshal.SizeOf<PROCESS_MEMORY_COUNTERS_EX>();

            if (GetProcessMemoryInfo(GetCurrentProcess(), out memoryCounters, memoryCounters.cb))
            {
                ret = memoryCounters.PrivateUsage;
            }

            Debug.WriteLine($"Memory usage:{ret}");
            return ret;
        }
    }


#else
    //Dummy implementation if _APP_PERFORMANCE_ is not defined
    internal static class MEMData
    {
        public static Int64 GetMEMUsage()
        {
            return 0;
        }
    }
#endif // _APP_PERFORMANCE_
}