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
using ChatterBox.Communication.Messages.Interfaces;

namespace ChatterBox.Communication.Messages.Relay
{
    public sealed class RelayMessage : IMessage
    {
        public int FromAvatar { get; set; }
        public string FromName { get; set; }
        public string FromUserId { get; set; }
        public string Payload { get; set; }
        public string Tag { get; set; }
        public string ToName { get; set; }
        public string ToUserId { get; set; }
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTimeOffset SentDateTimeUtc { get; set; }
    }
}