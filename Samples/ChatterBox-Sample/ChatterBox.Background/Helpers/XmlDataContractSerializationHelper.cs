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
using System.IO;
using System.Runtime.Serialization;
using System.Text;

namespace ChatterBox.Background.Helpers
{
    internal class XmlDataContractSerializationHelper
    {
        /// <summary>
        ///     Deserialize from xml.
        /// </summary>
        public static T FromXml<T>(string xml, IEnumerable<Type> knownTypes = null)
        {
            T value;
            using (Stream stream = new MemoryStream())
            {
                var data = Encoding.UTF8.GetBytes(xml);
                stream.Write(data, 0, data.Length);
                stream.Position = 0;
                var deserializer = knownTypes != null
                    ? new DataContractSerializer(typeof (T), knownTypes)
                    : new DataContractSerializer(typeof (T));
                value = (T) deserializer.ReadObject(stream);
            }
            return value;
        }

        /// <summary>
        ///     Serialize to xml.
        /// </summary>
        public static string ToXml<T>(T value, IEnumerable<Type> knownTypes = null)
        {
            var stream = new MemoryStream();
            var serializer = knownTypes != null
                ? new DataContractSerializer(typeof (T), knownTypes)
                : new DataContractSerializer(typeof (T));
            serializer.WriteObject(stream, value);
            stream.Position = 0;
            using (var sr = new StreamReader(stream))
            {
                return sr.ReadToEnd();
            }
        }
    }
}