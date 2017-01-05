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
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using ChatterBox.MVVM;

namespace ChatterBox.Services
{
    public class NtpService : DispatcherBindableBase
    {
        private const int MaxNtpRttProbeQuery = 100; // the attempt to get average RTT for NTP query/response

        private readonly Stopwatch _ntpResponseMonitor = new Stopwatch();

        private int _averageNtpRtt; //ms initialized to a invalid number
        private int _currentNtpQueryCount;
        private int _minNtpRtt = -1;

        private DispatcherTimer _ntpQueryTimer;
        private DispatcherTimer _ntpRttIntervalTimer;
        private DatagramSocket _ntpSocket;

        public NtpService(CoreDispatcher uiDispatcher) : base(uiDispatcher)
        {
        }

        public void AbortSync()
        {
            _ntpRttIntervalTimer?.Stop();
            _ntpQueryTimer?.Stop();
            _ntpResponseMonitor.Stop();
        }

        /// <summary>
        ///     Retrieve the current network time from ntp server  "time.windows.com".
        /// </summary>
        public async Task GetNetworkTime(string ntpServer)
        {
            _averageNtpRtt = 0; //reset

            _currentNtpQueryCount = 0; //reset
            _minNtpRtt = -1; //reset

            //NTP uses UDP
            _ntpSocket = new DatagramSocket();
            _ntpSocket.MessageReceived += OnNtpTimeReceived;


            if (_ntpQueryTimer == null)
            {
                _ntpQueryTimer = new DispatcherTimer();
                _ntpQueryTimer.Tick += NtpQueryTimeout;
                _ntpQueryTimer.Interval = new TimeSpan(0, 0, 5); //5 seconds
            }

            if (_ntpRttIntervalTimer == null)
            {
                _ntpRttIntervalTimer = new DispatcherTimer();
                _ntpRttIntervalTimer.Tick += SendNTPQueryTimerHandler;
                _ntpRttIntervalTimer.Interval = new TimeSpan(0, 0, 0, 0, 200); //200ms
            }

            _ntpQueryTimer.Start();

            try
            {
                //The UDP port number assigned to NTP is 123
                await _ntpSocket.ConnectAsync(new HostName(ntpServer), "123");
                _ntpRttIntervalTimer.Start();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"NtpSync: Exception when connect socket: {e.Message}");
                _ntpResponseMonitor.Stop();
                await ReportNtpSyncStatusAsync(false);
            }
        }

        public event Action OnNtpSyncFailed;

        public event Action<long> OnNtpTimeAvailable;

        /// <summary>
        ///     Report whether succeeded in sync with the ntp server or not.
        /// </summary>
        private async void NtpQueryTimeout(object sender, object e)
        {
            if (!_ntpResponseMonitor.IsRunning) return;
            _ntpResponseMonitor.Stop();
            await ReportNtpSyncStatusAsync(false);
        }

        /// <summary>
        ///     Event hander when receiving response from the ntp server.
        /// </summary>
        /// <param name="socket">The udp socket object which triggered this event </param>
        /// <param name="eventArguments">event information</param>
        private async void OnNtpTimeReceived(DatagramSocket socket,
            DatagramSocketMessageReceivedEventArgs eventArguments)
        {
            var currentRtt = (int) _ntpResponseMonitor.ElapsedMilliseconds;

            Debug.WriteLine($"NtpSync: current RTT {currentRtt}");


            _ntpResponseMonitor.Stop();

            if (_currentNtpQueryCount < MaxNtpRttProbeQuery)
            {
                //we only trace 'min' RTT within the RTT probe attempts
                if (_minNtpRtt == -1 || _minNtpRtt > currentRtt)
                {
                    _minNtpRtt = currentRtt;

                    if (_minNtpRtt == 0)
                        _minNtpRtt = 1; //in case we got response so  fast, consider it to be 1ms.
                }


                _averageNtpRtt = (_averageNtpRtt*(_currentNtpQueryCount - 1) + currentRtt)/_currentNtpQueryCount;

                if (_averageNtpRtt < 1)
                {
                    _averageNtpRtt = 1;
                }

                await RunOnUiThread(() =>
                {
                    _ntpQueryTimer.Stop();
                    _ntpRttIntervalTimer.Start();
                });

                return;
            }

            //if currentRTT is good enough, e.g.: closer to minRTT, then, we don't have to continue to query.
            if (currentRtt > (_averageNtpRtt + _minNtpRtt)/2)
            {
                await RunOnUiThread(() =>
                {
                    _ntpQueryTimer.Stop();
                    _ntpRttIntervalTimer.Start();
                });

                return;
            }


            var ntpData = new byte[48];

            eventArguments.GetDataReader().ReadBytes(ntpData);

            //Offset to get to the "Transmit Timestamp" field (time at which the reply
            //departed the server for the client, in 64-bit timestamp format."
            const byte serverReplyTime = 40;

            //Get the seconds part
            ulong intPart = BitConverter.ToUInt32(ntpData, serverReplyTime);

            //Get the seconds fraction
            ulong fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4);

            //Convert From big-endian to little-endian
            intPart = SwapEndianness(intPart);
            fractPart = SwapEndianness(fractPart);

            var milliseconds = intPart*1000 + fractPart*1000/0x100000000L;

            await RunOnUiThread(() => { OnNtpTimeAvailable?.Invoke((long) milliseconds + currentRtt/2); });

            socket.Dispose();
            await ReportNtpSyncStatusAsync(true, currentRtt);
        }


        /// <summary>
        ///     Report whether succeeded in sync with the ntp server or not.
        /// </summary>
        private async Task ReportNtpSyncStatusAsync(bool status, int rtt = 0)
        {
            MessageDialog dialog;
            if (status)
            {
                dialog = new MessageDialog(string.Format("Synced with ntp server. RTT time {0}ms", rtt));
            }
            else
            {
                OnNtpSyncFailed?.Invoke();
                dialog = new MessageDialog("Failed To sync with ntp server.");
            }

            await RunOnUiThread(async () =>
            {
                _ntpRttIntervalTimer.Stop();
                await dialog.ShowAsync();
            });
        }

        private async void SendNTPQueryTimerHandler(object sender, object e)
        {
            _currentNtpQueryCount++;
            // NTP message size - 16 bytes of the digest (RFC 2030)
            var ntpData = new byte[48];

            //Setting the Leap Indicator, Version Number and Mode values
            ntpData[0] = 0x1B; //LI = 0 (no warning), VN = 3 (IPv4 only), Mode = 3 (Client Mode)

            _ntpQueryTimer.Start();

            _ntpResponseMonitor.Restart();
            await _ntpSocket.OutputStream.WriteAsync(ntpData.AsBuffer());
        }


        private static uint SwapEndianness(ulong x)
        {
            return (uint) (((x & 0x000000ff) << 24) +
                           ((x & 0x0000ff00) << 8) +
                           ((x & 0x00ff0000) >> 8) +
                           ((x & 0xff000000) >> 24));
        }
    }
}