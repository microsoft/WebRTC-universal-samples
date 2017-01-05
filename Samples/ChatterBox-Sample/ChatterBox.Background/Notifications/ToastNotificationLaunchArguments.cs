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
using System.Collections.Generic;
using System.Runtime.Serialization;
using ChatterBox.Background.Helpers;

namespace ChatterBox.Background.Notifications
{
    [DataContract]
    public sealed class ToastNotificationLaunchArguments
    {
        public ToastNotificationLaunchArguments()
        {
            //XmlSerialization needs empty constructor
        }

        public ToastNotificationLaunchArguments(NotificationType type)
        {
            Type = type;
            Arguments = new Dictionary<ArgumentType, object>();
        }

        [DataMember]
        public IDictionary<ArgumentType, object> Arguments { get; set; }

        [DataMember]
        public NotificationType Type { get; private set; }

        public static ToastNotificationLaunchArguments FromXmlString(string xmlString)
        {
            ToastNotificationLaunchArguments result;
            try
            {
                result = XmlDataContractSerializationHelper.FromXml<ToastNotificationLaunchArguments>(xmlString);
            }
            catch (Exception)
            {
                result = null;
            }
            return result;
        }

        public string ToXmlString()
        {
            return XmlDataContractSerializationHelper.ToXml(this);
        }
    }
}