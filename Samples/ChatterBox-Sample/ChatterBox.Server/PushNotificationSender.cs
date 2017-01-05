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
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;

namespace ChatterBox.Server
{
    public class PushNotificationSender
    {
        private const string ChannelUriKey = "channelURI";

        private const string PayloadKey = "payload";
        private readonly List<Dictionary<string, string>> _listOfNotificationsForSending;
        private readonly NotificationType _notificationType;

        public PushNotificationSender() : this(null, NotificationType.Raw)
        {
        }

        public PushNotificationSender(string chanellUri) : this(chanellUri, NotificationType.Raw)
        {
        }

        public PushNotificationSender(string chanellUri, NotificationType type)
        {
            ChannelUri = chanellUri;
            _notificationType = type;
            _listOfNotificationsForSending = new List<Dictionary<string, string>>();
            WNSAuthentication.Instance.OnAuthenticated += OnAuthenticated;
        }

        public string ChannelUri { get; set; }

        public event Action OnChannelUriExpired;

        public async Task SendNotificationAsync(string payload)
        {
            await SendNotification(ChannelUri, payload, _notificationType);
        }


        private static string GetHeaderType(NotificationType type)
        {
            string ret;
            switch (type)
            {
                case NotificationType.Badge:
                    ret = "wns/badge";
                    break;
                case NotificationType.Tile:
                    ret = "wns/tile";
                    break;
                case NotificationType.Toast:
                    ret = "wns/toast";
                    break;
                case NotificationType.Raw:
                    ret = "wns/raw";
                    break;
                default:
                    ret = "wns/raw";
                    break;
            }
            return ret;
        }

        private void HandleError(HttpStatusCode errorCode, string channelUri, string payload)
        {
            switch (errorCode)
            {
                case HttpStatusCode.Unauthorized:
                    StoreNotificationForSending(channelUri, payload);
                    break;
                case HttpStatusCode.Gone:
                case HttpStatusCode.NotFound:
                    OnChannelUriExpired?.Invoke();
                    break;

                case HttpStatusCode.NotAcceptable:
                    break;
            }
        }

        private async void OnAuthenticated()
        {
            while (_listOfNotificationsForSending.Count > 0)
            {
                var notification = _listOfNotificationsForSending[0];
                _listOfNotificationsForSending.RemoveAt(0);
                await SendNotification(notification[ChannelUriKey], notification[PayloadKey]);
            }
        }

        private async Task SendNotification(string channelUri, string payload,
            NotificationType type = NotificationType.Raw)
        {
            if (WNSAuthentication.Instance.oAuthToken.AccessToken != null &&
                !WNSAuthentication.Instance.IsRefreshInProgress)
            {
                using (var client = new WebClient())
                {
                    SetHeaders(type, client);
                    try
                    {
                        await client.UploadStringTaskAsync(new Uri(channelUri), payload);
                    }
                    catch (WebException webException)
                    {
                        if (webException.Response != null)
                            HandleError(((HttpWebResponse)webException.Response).StatusCode, channelUri, payload);
                        Debug.WriteLine($"Failed WNS authentication. Error: {webException.Message}");
                    }
                    catch (Exception)
                    {
                        HandleError(HttpStatusCode.Unauthorized, channelUri, payload);
                    }
                }
            }
            else
            {
                StoreNotificationForSending(channelUri, payload);
            }
        }

        private static void SetHeaders(NotificationType type, WebClient client)
        {
            client.Headers.Add("X-WNS-Type", GetHeaderType(type));
            client.Headers.Add("Authorization", $"Bearer {WNSAuthentication.Instance.oAuthToken.AccessToken}");
        }

        private void StoreNotificationForSending(string channelUri, string payload)
        {
            var notificationDict = new Dictionary<string, string>
            {
                [ChannelUriKey] = channelUri,
                [PayloadKey] = payload
            };
            _listOfNotificationsForSending.Add(notificationDict);
        }
    }
}