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
using Windows.UI.Xaml.Media;

namespace ChatterBox.Controls
{
    public interface IInstantMessage
    {
        /// <summary>
        ///     The text body of the instant message
        /// </summary>
        string Body { get; }

        /// <summary>
        ///     Indicates the DateTime for the delivery of the instant message
        /// </summary>
        DateTime DeliveredAt { get; }

        /// <summary>
        ///     Highlights the instant message. Can be used to indicate an unread message.
        /// </summary>
        bool IsHighlighted { get; set; }

        /// <summary>
        ///     Indicates if the local peer is the sender of the instant message
        /// </summary>
        bool IsSender { get; }

        /// <summary>
        ///     Gets the name of the sender
        /// </summary>
        string SenderName { get; }

        /// <summary>
        ///     Gets the profile image source for the sender
        /// </summary>
        ImageSource SenderProfileSource { get; }
    }
}