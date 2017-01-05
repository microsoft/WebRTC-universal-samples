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

using System.Runtime.InteropServices;

namespace ChatterBox.Background.Call.Utils
{
    [StructLayout(LayoutKind.Sequential, Size = 40)]
    internal struct PROCESS_MEMORY_COUNTERS_EX
    {
        public uint cb; // The size of the structure, in bytes (DWORD).
        public uint PageFaultCount; // The number of page faults (DWORD).
        public uint PeakWorkingSetSize; // The peak working set size, in bytes (SIZE_T).
        public uint WorkingSetSize; // The current working set size, in bytes (SIZE_T).
        public uint QuotaPeakPagedPoolUsage; // The peak paged pool usage, in bytes (SIZE_T).
        public uint QuotaPagedPoolUsage; // The current paged pool usage, in bytes (SIZE_T).
        public uint QuotaPeakNonPagedPoolUsage; // The peak nonpaged pool usage, in bytes (SIZE_T).
        public uint QuotaNonPagedPoolUsage; // The current nonpaged pool usage, in bytes (SIZE_T).

        public uint PagefileUsage;
        // The Commit Charge value in bytes for this process (SIZE_T). Commit Charge is the total amount of memory that the memory manager has committed for a running process.

        public uint PeakPagefileUsage;
        // The peak value in bytes of the Commit Charge during the lifetime of this process (SIZE_T).

        public uint PrivateUsage;
    }
}