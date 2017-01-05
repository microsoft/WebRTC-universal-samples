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

using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace ChatterBox.Controls
{
    public class CommunicationControl : Control
    {
        /// <summary>
        ///     The <see cref="ContactListHeaderContent" /> dependency property's name.
        /// </summary>
        public const string ContactListHeaderContentPropertyName = nameof(ContactListHeaderContent);


        /// <summary>
        ///     The <see cref="ContactListHeaderTemplate" /> dependency property's name.
        /// </summary>
        public const string ContactListHeaderTemplatePropertyName = nameof(ContactListHeaderTemplate);

        /// <summary>
        ///     The <see cref="Conversations" /> dependency property's name.
        /// </summary>
        public const string ConversationsPropertyName = nameof(Conversations);


        /// <summary>
        ///     The <see cref="LayoutRootStyle" /> dependency property's name.
        /// </summary>
        public const string LayoutRootStylePropertyName = nameof(LayoutRootStyle);

        /// <summary>
        ///     The <see cref="SelectedConversation" /> dependency property's name.
        /// </summary>
        public const string SelectedConversationPropertyName = nameof(SelectedConversation);

        /// <summary>
        ///     Identifies the <see cref="Conversations" /> dependency property.
        /// </summary>
        public static readonly DependencyProperty ConversationsProperty = DependencyProperty.Register(
            ConversationsPropertyName,
            typeof (IEnumerable),
            typeof (CommunicationControl),
            new PropertyMetadata(null));

        /// <summary>
        ///     Identifies the <see cref="SelectedConversation" /> dependency property.
        /// </summary>
        public static readonly DependencyProperty SelectedConversationProperty = DependencyProperty.Register(
            SelectedConversationPropertyName,
            typeof (IConversation),
            typeof (CommunicationControl),
            new PropertyMetadata(null));

        /// <summary>
        ///     Identifies the <see cref="LayoutRootStyle" /> dependency property.
        /// </summary>
        public static readonly DependencyProperty LayoutRootStyleProperty = DependencyProperty.Register(
            LayoutRootStylePropertyName,
            typeof (Style),
            typeof (CommunicationControl),
            new PropertyMetadata(null));

        /// <summary>
        ///     Identifies the <see cref="ContactListHeaderContent" /> dependency property.
        /// </summary>
        public static readonly DependencyProperty ContactListHeaderContentProperty = DependencyProperty.Register(
            ContactListHeaderContentPropertyName,
            typeof (object),
            typeof (CommunicationControl),
            new PropertyMetadata(null));

        /// <summary>
        ///     Identifies the <see cref="ContactListHeaderTemplate" /> dependency property.
        /// </summary>
        public static readonly DependencyProperty ContactListHeaderTemplateProperty = DependencyProperty.Register(
            ContactListHeaderTemplatePropertyName,
            typeof (DataTemplate),
            typeof (CommunicationControl),
            new PropertyMetadata(null));


        public CommunicationControl()
        {
            DefaultStyleKey = typeof (CommunicationControl);
            Conversations = new ObservableCollection<IConversation>();
        }

        /// <summary>
        ///     Gets or sets the value of the <see cref="ContactListHeaderContent" />
        ///     property. This is a dependency property.
        /// </summary>
        public object ContactListHeaderContent
        {
            get { return GetValue(ContactListHeaderContentProperty); }
            set { SetValue(ContactListHeaderContentProperty, value); }
        }

        /// <summary>
        ///     Gets or sets the value of the <see cref="ContactListHeaderTemplate" />
        ///     property. This is a dependency property.
        /// </summary>
        public DataTemplate ContactListHeaderTemplate
        {
            get { return (DataTemplate) GetValue(ContactListHeaderTemplateProperty); }
            set { SetValue(ContactListHeaderTemplateProperty, value); }
        }

        /// <summary>
        ///     Gets or sets the value of the <see cref="Conversations" />
        ///     property. This is a dependency property.
        /// </summary>
        public IEnumerable<IConversation> Conversations
        {
            get { return (IEnumerable<IConversation>) GetValue(ConversationsProperty); }
            set { SetValue(ConversationsProperty, value); }
        }

        /// <summary>
        ///     Gets or sets the value of the <see cref="LayoutRootStyle" />
        ///     property. This is a dependency property.
        /// </summary>
        public Style LayoutRootStyle
        {
            get { return (Style) GetValue(LayoutRootStyleProperty); }
            set { SetValue(LayoutRootStyleProperty, value); }
        }

        /// <summary>
        ///     Gets or sets the value of the <see cref="SelectedConversation" />
        ///     property. This is a dependency property.
        /// </summary>
        public IConversation SelectedConversation
        {
            get { return (IConversation) GetValue(SelectedConversationProperty); }
            set { SetValue(SelectedConversationProperty, value); }
        }
    }
}