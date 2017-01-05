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

using System.Collections.Generic;
using System.Windows.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace ChatterBox.Controls
{
    public interface IConversation
    {
        /// <summary>
        ///     Gets the ICommand used to answer a call
        /// </summary>
        ICommand AnswerCommand { get; }

        /// <summary>
        ///     Gets the ICommand used to initiate an audio call
        /// </summary>
        ICommand AudioCallCommand { get; }

        /// <summary>
        ///     Gets the current state of the call
        /// </summary>
        CallState CallState { get; }


        /// <summary>
        ///     Gets the ICommand used to close the current conversation by unselecting it.
        /// </summary>
        ICommand CloseConversationCommand { get; }


        /// <summary>
        ///     Gets the ICommand used to end a call
        /// </summary>
        ICommand HangupCommand { get; }

        /// <summary>
        ///     Gets/Sets the instant message that is being composed
        /// </summary>
        string InstantMessage { get; set; }

        /// <summary>
        ///     Gets the list of instant messages in the conversation
        /// </summary>
        IEnumerable<IInstantMessage> InstantMessages { get; }

        /// <summary>
        ///     Gets the highlight state of the conversation.
        ///     This should be 'True' if the conversation has an event (ex: missed call or IM)
        /// </summary>
        bool IsHighlighted { get; }

        /// <summary>
        ///     Gets the state of the local peer's microphone.
        ///     This should be used to indicate if the microphone is muted.
        /// </summary>
        bool IsMicrophoneEnabled { get; }

        /// <summary>
        ///     Presence state for the remote peer
        /// </summary>
        bool IsOnline { get; }

        /// <summary>
        ///     Gets the remote peer's video availability
        /// </summary>
        bool IsPeerVideoAvailable { get; }

        /// <summary>
        ///     Gets the local peer's video availability
        /// </summary>
        bool IsSelfVideoAvailable { get; }

        /// <summary>
        ///     Gets the state of the local party's video stream.
        ///     This should be used to indicate if the video stream is enabled.
        /// </summary>
        bool IsVideoEnabled { get; }


        /// <summary>
        ///     Gets the UIElement used to render video for the local peer
        /// </summary>
        UIElement LocalVideoRenderer { get; }

        /// <summary>
        ///     Gets the ICommand used to mute the local peer's microphone
        /// </summary>
        ICommand MuteMicrophoneCommand { get; }

        /// <summary>
        ///     The name of the remote peer
        /// </summary>
        string Name { get; }

        /// <summary>
        ///     Source for the local peer's profile image
        /// </summary>
        ImageSource OwnProfileSource { get; }

        /// <summary>
        ///     Source for the remote peer's profile image
        /// </summary>
        ImageSource ProfileSource { get; }


        /// <summary>
        ///     Gets the ICommand used to reject a call
        /// </summary>
        ICommand RejectCommand { get; }

        /// <summary>
        ///     Gets the UIElement used to render video for the remote peer
        /// </summary>
        UIElement RemoteVideoRenderer { get; }

        /// <summary>
        ///     Gets the ICommand used to send the current message
        /// </summary>
        ICommand SendInstantMessageCommand { get; }

        /// <summary>
        ///     Gets the ICommand used to toggle the local peer's video stream on and off
        /// </summary>
        ICommand SwitchVideoCommand { get; }

        /// <summary>
        ///     Gets the ICommand used to unmute the local peer's microphone
        /// </summary>
        ICommand UnMuteMicrophoneCommand { get; }

        /// <summary>
        ///     Gets the ICommand used to initiate a video call
        /// </summary>
        ICommand VideoCallCommand { get; }
    }
}