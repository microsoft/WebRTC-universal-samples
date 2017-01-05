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

namespace ChatterBox.Communication.Helpers
{
    public sealed class InvocationResult
    {
        public string ErrorMessage { get; set; }
        public bool Invoked { get; set; }
        public object Result { get; set; }
        public Type ResultType { get; set; }
    }
}