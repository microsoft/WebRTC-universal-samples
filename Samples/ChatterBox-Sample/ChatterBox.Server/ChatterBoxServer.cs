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
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Timers;
using ChatterBox.Communication.Messages.Registration;
using Common.Logging;

namespace ChatterBox.Server
{
    public class ChatterBoxServer
    {
        private readonly Timer _heartBeatTimer = new Timer
        {
            Interval = 10000
        };

        public ChatterBoxServer(int port = 50000)
        {
            Port = port;
            _heartBeatTimer.Elapsed += HeartBeatTimer_Elapsed;
        }

        public ConcurrentDictionary<string, Domain> Domains { get; } = new ConcurrentDictionary<string, Domain>();
        private ILog Logger => LogManager.GetLogger(nameof(ChatterBoxServer));
        public int Port { get; }

        public ConcurrentDictionary<Guid, UnregisteredConnection> UnregisteredConnections { get; } =
            new ConcurrentDictionary<Guid, UnregisteredConnection>();

        public void Run()
        {
            WNSAuthentication.Instance.AuthenticateWithWNS();

            Logger.Info($"Starting TCP listener on port {Port}");
            var listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();

            Logger.Info($"Starting heartbeat timer with interval {_heartBeatTimer.Interval}");
            _heartBeatTimer.Start();


            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        HandleNewConnection(await listener.AcceptTcpClientAsync());
                    }
                    catch (Exception ex)
                    {
                        Logger.Fatal("Exception occred when creating a new client session", ex);
                    }
                }
            });
        }

        private Domain GetOrAddDomain(string key)
        {
            return Domains.GetOrAdd(key.ToUpper(), new Domain
            {
                Name = key.ToUpper()
            });
        }

        private void HandleNewConnection(TcpClient tcpClient)
        {
            Task.Run(() =>
            {
                var connection = new UnregisteredConnection(tcpClient);
                Logger.Info($"{connection} connected.");
                connection.OnRegister += UnregisteredConnection_OnRegister;
                UnregisteredConnections.GetOrAdd(connection.Id, connection);
                connection.WaitForRegistration();
            });
        }

        private async void HeartBeatTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            var domains = Domains.Select(s => s.Value).ToList();
            foreach (var domain in domains)
            {
                await domain.OnHeartBeatAsync();
            }
        }

        private async void UnregisteredConnection_OnRegister(UnregisteredConnection sender, Registration message)
        {
            var domain = GetOrAddDomain(message.Domain);
            UnregisteredConnection connection;
            UnregisteredConnections.TryRemove(sender.Id, out connection);
            connection.OnRegister -= UnregisteredConnection_OnRegister;
            await domain.HandleRegistrationAsync(connection, message);
        }
    }
}