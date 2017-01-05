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
using System.Linq;
using System.Reflection;
using System.Text;
using ChatterBox.Communication.Messages.Interfaces;
using Newtonsoft.Json;

namespace ChatterBox.Communication.Helpers
{
    public sealed class ChannelWriteHelper
    {
        private readonly Type _target;

        public ChannelWriteHelper(Type target)
        {
            _target = target;
        }

        public string FormatOutput(object argument, string method)
        {
            var message = argument as IMessage;
            if (message != null)
            {
                message.SentDateTimeUtc = DateTimeOffset.UtcNow;
            }


            if (method == null) return null;
            var methodDefinition = _target.GetRuntimeMethods().Single(s => s.Name == method);
            if (methodDefinition == null) return null;

            var messageBuilder = new StringBuilder();
            messageBuilder.Append(method);
            if (argument == null) return messageBuilder.ToString();
            var serializedArgument = JsonConvert.SerializeObject(argument);
            messageBuilder.Append(" ");
            messageBuilder.Append(serializedArgument);
            return messageBuilder.ToString();
        }
    }
}