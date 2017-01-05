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
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Foundation;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using ChatterBox.Background.Notifications;
using Buffer = Windows.Storage.Streams.Buffer;

namespace ChatterBox.Background.Tasks
{
    public sealed class SignalingTask : IBackgroundTask
    {
        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            using (new BackgroundTaskDeferralWrapper(taskInstance.GetDeferral()))
            {
                try
                {
                    var details = (SocketActivityTriggerDetails) taskInstance.TriggerDetails;
                    switch (details.Reason)
                    {
                        case SocketActivityTriggerReason.SocketActivity:
                            string request = null;
                            using (var socketOperation = Hub.Instance.SignalingSocketChannel.SocketOperation)
                            {
                                if (socketOperation.Socket != null)
                                {
                                    var socket = socketOperation.Socket;

                                    const uint length = 65536;
                                    var readBuf = new Buffer(length);

                                    var readOp = socket.InputStream.ReadAsync(readBuf, length,
                                        InputStreamOptions.Partial);
                                    // This delay is to limit how long we wait for reading.
                                    // StreamSocket has no ability to peek to see if there's any
                                    // data waiting to be read.  So we have to limit it this way.
                                    for (var i = 0; i < 100 && readOp.Status == AsyncStatus.Started; ++i)
                                    {
                                        await Task.Delay(10);
                                    }
                                    await socket.CancelIOAsync();

                                    try
                                    {
                                        var localBuffer = await readOp;
                                        var dataReader = DataReader.FromBuffer(localBuffer);
                                        request = dataReader.ReadString(dataReader.UnconsumedBufferLength);
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"ReadAsync exception: {ex}");
                                    }
                                }
                            }
                            if (request != null)
                            {
                                await Hub.Instance.SignalingClient.HandleRequest(request);
                            }
                            break;
                        case SocketActivityTriggerReason.KeepAliveTimerExpired:
                            await Hub.Instance.SignalingClient.ClientHeartBeatAsync();
                            break;
                        case SocketActivityTriggerReason.SocketClosed:
                            await Hub.Instance.SignalingClient.ServerConnectionErrorAsync();
                            //ToastNotificationService.ShowToastNotification("Disconnected.");
                            break;
                    }
                }
                catch (Exception exception)
                {
                    await Hub.Instance.SignalingClient.ServerConnectionErrorAsync();
                    ToastNotificationService.ShowToastNotification(string.Format("Error in SignalingTask: {0}",
                        exception.Message));
                }
            }
        }
    }
}