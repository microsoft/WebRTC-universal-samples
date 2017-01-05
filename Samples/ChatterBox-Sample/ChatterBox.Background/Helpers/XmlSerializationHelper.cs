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

using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace ChatterBox.Background.Helpers
{
    internal class XmlSerializationHelper
    {
        /// <summary>
        ///     Deserialize from xml.
        /// </summary>
        public static T FromXml<T>(string xml)
        {
            var serializer = new XmlSerializer(typeof (T));
            T value;
            using (var stringReader = new StringReader(xml))
            {
                var deserialized = serializer.Deserialize(stringReader);
                value = (T) deserialized;
            }

            return value;
        }

        /// <summary>
        ///     Serialize to xml.
        /// </summary>
        public static string ToXml<T>(T value)
        {
            var serializer = new XmlSerializer(typeof (T));
            var stringBuilder = new StringBuilder();
            var settings = new XmlWriterSettings
            {
                Indent = true,
                OmitXmlDeclaration = true
            };

            using (var xmlWriter = XmlWriter.Create(stringBuilder, settings))
            {
                serializer.Serialize(xmlWriter, value);
            }
            return stringBuilder.ToString();
        }
    }
}