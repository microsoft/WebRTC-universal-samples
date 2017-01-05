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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChatterBox.Communication.Messages.Interfaces;
using ChatterBox.Communication.Messages.Peers;
using ChatterBox.Communication.Messages.Registration;
using ChatterBox.Communication.Messages.Relay;
using ChatterBox.Server.Helpers;
using Common.Logging;

namespace ChatterBox.Server
{
    public class Domain
    {
        public ConcurrentDictionary<string, RegisteredClient> Clients { get; } =
            new ConcurrentDictionary<string, RegisteredClient>();

        private ILog Logger
            => LogManager.GetLogger(string.IsNullOrWhiteSpace(Name) ? nameof(Domain) : $"Domain[{Name}]");

        public string Name { get; set; }

        public async Task<bool> HandleRegistrationAsync(UnregisteredConnection unregisteredConnection,
            Registration message)
        {
            Logger.Info($"Handling the registration of connection {unregisteredConnection}");
            RegisteredClient registeredClient;
            if (Clients.ContainsKey(message.UserId))
            {
                if (Clients.TryGetValue(message.UserId, out registeredClient))
                {
                    Logger.Debug($"Client identified. {registeredClient}");
                }
                else
                {
                    Logger.Warn("Error in retrieving client.");
                    return false;
                }
            }
            else
            {
                registeredClient = new RegisteredClient
                {
                    UserId = message.UserId,
                    Domain = Name,
                    Name = message.Name,
                    Avatar = Clients.Count + 1
                };
                registeredClient.OnConnected += RegisteredClient_OnConnected;
                registeredClient.OnDisconnected += RegisteredClient_OnDisconnected;
                registeredClient.OnGetPeerList += RegisteredClient_OnGetPeerList;
                registeredClient.OnRelayMessage += RegisteredClient_OnRelayMessage;

                if (Clients.TryAdd(registeredClient.UserId, registeredClient))
                {
                    Logger.Info($"Registered new client. {registeredClient}");
                }
                else
                {
                    Logger.Warn("Could not register new client.");
                    return false;
                }
            }

            await registeredClient.SetActiveConnectionAsync(unregisteredConnection, message);
            return true;
        }

        public async Task OnHeartBeatAsync()
        {
            var clients = Clients.Select(s => s.Value).ToList();
            foreach (var client in clients)
            {
                await client.ServerHeartBeatAsync().CastToTask();
            }
        }

        private PeerUpdate GetClientInformation(RegisteredClient registeredClient)
        {
            return new PeerUpdate
            {
                PeerData = new PeerData
                {
                    UserId = registeredClient.UserId,
                    Name = registeredClient.Name,
                    IsOnline = registeredClient.IsOnline,
                    Avatar = registeredClient.Avatar
                },
                SentDateTimeUtc = DateTimeOffset.UtcNow
            };
        }

        private List<RegisteredClient> GetPeers(RegisteredClient sender)
        {
            return Clients.Where(s => s.Key != sender.UserId).Select(s => s.Value).ToList();
        }

        private async void RegisteredClient_OnConnected(RegisteredClient sender)
        {
            var peers = GetPeers(sender);
            foreach (var registeredClient in peers)
            {
                await registeredClient.OnPeerPresenceAsync(GetClientInformation(sender)).CastToTask();
            }
        }

        private async void RegisteredClient_OnDisconnected(RegisteredClient sender)
        {
            var peers = GetPeers(sender);
            foreach (var registeredClient in peers)
            {
                await registeredClient.OnPeerPresenceAsync(GetClientInformation(sender)).CastToTask();
            }
        }

        private async void RegisteredClient_OnGetPeerList(RegisteredClient sender, IMessage message)
        {
            await sender.OnPeerListAsync(new PeerList
            {
                ReplyFor = message.Id,
                Peers = GetPeers(sender).Select(s => GetClientInformation(s).PeerData).ToArray()
            }).CastToTask();
        }

        private async void RegisteredClient_OnRelayMessage(RegisteredClient sender, RelayMessage message)
        {
            RegisteredClient receiver;
            if (!Clients.TryGetValue(message.ToUserId, out receiver))
            {
                return;
            }
            message.FromUserId = sender.UserId;
            message.FromName = sender.Name;
            message.FromAvatar = sender.Avatar;
            await receiver.ServerRelayAsync(message).CastToTask();
        }
    }
}