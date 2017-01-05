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
using System.Runtime.InteropServices;

namespace ChatterBox.Background.Call.Utils
{
#if _APP_PERFORMANCE_
    internal static class CPUData
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct FileTime
        {
            public readonly uint Low;
            public readonly uint High;
        }

        /// <summary>
        ///     Uitility function to convert FileTime to uint64
        /// </summary>
        private static ulong ToUInt64(FileTime time)
        {
            return ((ulong) time.High << 32) + time.Low;
        }

#if WINDOWS_PHONE_APP || USE_WIN10_PHONE_DLL
        [DllImport("api-ms-win-core-sysinfo-l1-2-0.dll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = true)]
        private static extern IntPtr GetCurrentProcess();
#else
        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetCurrentProcess();
#endif

#if WINDOWS_PHONE_APP || USE_WIN10_PHONE_DLL
        [DllImport("api-ms-win-core-sysinfo-l1-2-0.dll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetProcessTimes(
            IntPtr hProcess,
            out FileTime lpCreationTime,
            out FileTime lpExitTime,
            out FileTime lpKernelTime,
            out FileTime lpUserTime);
#else
        [DllImport("kernel32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetProcessTimes(
            IntPtr hProcess,
            out FileTime lpCreationTime,
            out FileTime lpExitTime,
            out FileTime lpKernelTime,
            out FileTime lpUserTime);
#endif


        public struct ProcessTimes
        {
            public ulong CreationTime;
            public ulong ExitTime;
            public ulong KernelTime;
            public ulong UserTime;
        }

        /// <summary>
        ///     Get the cpu time for this process
        /// </summary>
        public static ProcessTimes GetProcessTimes()
        {
            FileTime creation, exit, kernel, user;

            if (!GetProcessTimes(GetCurrentProcess(),
                out creation, out exit, out kernel, out user))
                throw new Exception(":'(");

            return new ProcessTimes
            {
                CreationTime = ToUInt64(creation),
                ExitTime = ToUInt64(exit),
                KernelTime = ToUInt64(kernel),
                UserTime = ToUInt64(user)
            };
        }

        /// <summary>
        ///     Calculate CPU usage, e.g.: this process time vs system process time
        ///     return the CPU usage in percentage
        /// </summary>
        public static double GetCPUUsage()
        {
            var ret = 0.0;

            //retrieve process time
            var processTimes = GetProcessTimes();

            var currentProcessTime = processTimes.KernelTime + processTimes.UserTime;

            //retrieve system time
            // get number of CPU cores, then, check system time for every CPU core
            if (numberOfProcessors == 0)
            {
                SystemInfo info;
                GetSystemInfo(out info);

                Debug.WriteLine(info.NumberOfProcessors);

                numberOfProcessors = info.NumberOfProcessors;
            }

            var size = Marshal.SizeOf<SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION>();

            size = (int) (size*numberOfProcessors);

            var systemProcessInfoBuff = Marshal.AllocHGlobal(size); // should be more than adequate
            uint length = 0;
            var result = NtQuerySystemInformation(SYSTEM_INFORMATION_CLASS.SystemProcessorPerformanceInformation,
                systemProcessInfoBuff, (uint) size, out length);

            if (result != NtStatus.Success)
            {
                Debug.WriteLine($"Failed to obtain processor performance information ({result})");
                return ret;
            }
            ulong currentSystemTime = 0;
            var systemProcInfoData = systemProcessInfoBuff;
            for (var i = 0; i < numberOfProcessors; i++)
            {
                var processPerInfo = Marshal.PtrToStructure<SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION>(systemProcInfoData);

                currentSystemTime += processPerInfo.KernelTime + processPerInfo.UserTime;
            }

            //we need to at least measure twice
            if (previousProcessTime != 0 && previousSystemTIme != 0)
            {
                ret = (currentProcessTime - previousProcessTime)/(double) (currentSystemTime - previousSystemTIme)*100.0;
            }

            previousProcessTime = currentProcessTime;

            previousSystemTIme = currentSystemTime;

            Debug.WriteLine($"CPU usage: {ret}%");

            return ret;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION
        {
            public ulong IdleTime;
            public ulong KernelTime;
            public ulong UserTime;
            public ulong Reserved10;
            public ulong Reserved11;
            public ulong Reserved2;
        }

        /// <summary>Retrieves the specified system information.</summary>
        /// <param name="InfoClass">indicate the kind of system information to be retrieved</param>
        /// <param name="Info">a buffer that receives the requested information</param>
        /// <param name="Size">The allocation size of the buffer pointed to by Info</param>
        /// <param name="Length">If null, ignored.  Otherwise tells you the size of the information returned by the kernel.</param>
        /// <returns>Status Information</returns>
        /// http://msdn.microsoft.com/en-us/library/windows/desktop/ms724509%28v=vs.85%29.aspx
        [DllImport("ntdll.dll", SetLastError = false, ExactSpelling = true)]
        private static extern NtStatus NtQuerySystemInformation(SYSTEM_INFORMATION_CLASS InfoClass, IntPtr Info,
            uint Size, out uint Length);


#if WINDOWS_PHONE_APP || USE_WIN10_PHONE_DLL
        [DllImport("api-ms-win-core-sysinfo-l1-2-0.dll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = true)]
        private static extern void GetSystemInfo(out SystemInfo Info);
#else
        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = false)]
        private static extern void GetSystemInfo(out SystemInfo Info);
#endif


        private static uint numberOfProcessors;
        private static ulong previousProcessTime;
        private static ulong previousSystemTIme;
    }
#else
    internal static class CPUData
    {
        public static double GetCPUUsage()
        {
            return 0;
        }
    }
#endif
}