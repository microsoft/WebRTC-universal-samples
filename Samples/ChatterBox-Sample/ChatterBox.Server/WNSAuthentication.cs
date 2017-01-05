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
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Timers;
using System.Web;

namespace ChatterBox.Server
{
    public sealed class WNSAuthentication
    {
        private const string AccessScope = "notify.windows.com";
        private const string AccessTokenUrl = "https://login.live.com/accesstoken.srf";

        private const string PayloadFormat = "grant_type=client_credentials&client_id={0}&client_secret={1}&scope={2}";
        private const string UrlEncoded = "application/x-www-form-urlencoded";
        //These values are obtained from Windows Dev Center Dashboard. THESE VALUES MUST NOT BE PUBLIC.
        private const string WNS_PACKAGE_SECURITY_IDENTIFIER =
            "ms-app://s-1-15-2-480391716-3273138829-3268582380-534771994-102819520-3620776998-3780754916";

        private const string WNS_SECRET_KEY = "u/w9VhqrzVZjzl4TznCsG/FddOuDHIkX";

        private static readonly object _oAuthTokenLock = new object();
        private static readonly object _refreshLock = new object();

        private static readonly Lazy<WNSAuthentication> lazy =
            new Lazy<WNSAuthentication>(() => new WNSAuthentication());

        private bool _isRefreshInProgress;
        private OAuthToken _oAuthToken;

        private Timer timerTokenRefresh;

        private WNSAuthentication()
        {
        }

        public static WNSAuthentication Instance
        {
            get { return lazy.Value; }
        }

        public bool IsRefreshInProgress
        {
            get
            {
                lock (_refreshLock)
                {
                    return _isRefreshInProgress;
                }
            }
            set
            {
                lock (_refreshLock)
                {
                    _isRefreshInProgress = value;
                }
            }
        }

        public OAuthToken oAuthToken
        {
            get
            {
                lock (_oAuthTokenLock)
                {
                    return _oAuthToken;
                }
            }
            set
            {
                lock (_oAuthTokenLock)
                {
                    _oAuthToken = value;
                }
            }
        }

        public async void AuthenticateWithWNS()
        {
            IsRefreshInProgress = true;

            var urlEncodedSid = HttpUtility.UrlEncode(WNS_PACKAGE_SECURITY_IDENTIFIER);
            var urlEncodedSecret = HttpUtility.UrlEncode(WNS_SECRET_KEY);

            var body = string.Format(PayloadFormat, urlEncodedSid, urlEncodedSecret, AccessScope);

            string response = null;
            Exception exception = null;
            using (var client = new WebClient())
            {
                client.Headers.Add("Content-Type", UrlEncoded);
                try
                {
                    response = await client.UploadStringTaskAsync(new Uri(AccessTokenUrl), body);
                }
                catch (Exception e)
                {
                    exception = e;
                    Debug.WriteLine(string.Format("Failed WNS authentication. Error: {0}", e.Message));
                }
            }

            if (exception == null && response != null)
            {
                oAuthToken = GetOAuthTokenFromJson(response);
                ScheduleTokenRefreshing();
            }

            IsRefreshInProgress = false;
            OnAuthenticated?.Invoke();
        }

        public event Action OnAuthenticated;

        private OAuthToken GetOAuthTokenFromJson(string jsonString)
        {
            using (var ms = new MemoryStream(Encoding.Unicode.GetBytes(jsonString)))
            {
                var ser = new DataContractJsonSerializer(typeof (OAuthToken));
                var oAuthToken = (OAuthToken) ser.ReadObject(ms);
                return oAuthToken;
            }
        }

        private void RefreshToken(object sender, ElapsedEventArgs e)
        {
            ResetTimer(timerTokenRefresh);
            AuthenticateWithWNS();
        }

        private void ResetTimer(Timer timer)
        {
            if (timer != null)
            {
                timer.Stop();
                timer.Dispose();
                timer = null;
            }
        }

        private void ScheduleTokenRefreshing()
        {
            if (oAuthToken != null)
            {
                timerTokenRefresh = new Timer((oAuthToken.ExpireTime - 600)*1000);
                timerTokenRefresh.AutoReset = false;
                timerTokenRefresh.Enabled = true;
                timerTokenRefresh.Elapsed += RefreshToken;
            }
        }
    }
}