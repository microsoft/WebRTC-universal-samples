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

using ChatterBox.Communication.Messages.Interfaces;

namespace ChatterBox.Server
{
    public class RegisteredClientMessageQueueItem
    {
        public bool IsDelivered { get; set; }
        public bool IsSent { get; set; }
        public IMessage Message { get; set; }
        public string Method { get; set; }
        public string SerializedMessage { get; set; }
    }
}