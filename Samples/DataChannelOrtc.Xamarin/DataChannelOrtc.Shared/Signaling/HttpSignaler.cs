using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DataChannelOrtc.Shared.Signaling
{
    /// <summary>
    /// Signaller instance is used to fire connection events.
    /// </summary>
    public class Signaler
    {
        // Connection events
        public event EventHandler SignedIn;
        public event EventHandler Disconnected;
        public event EventHandler<(int id, string name)> PeerConnected;
        public event EventHandler<int> PeerDisconnected;
        public event EventHandler<int> PeerHangup;
        public event EventHandler<(int peer_id, string message)> MessageFromPeer;
        public event EventHandler ServerConnectionFailure;

        protected void OnSignedIn()
        {
            if (SignedIn != null)
                SignedIn(this, null);
        }
        protected void OnDisconnected()
        {
            if (Disconnected != null)
                Disconnected(this, null);
        }
        protected void OnPeerConnected(int id, string name)
        {
            if (PeerConnected != null)
                PeerConnected(this, (id, name));
        }
        protected void OnPeerDisconnected(int id)
        {
            if (PeerDisconnected != null)
                PeerDisconnected(this, id);
        }
        protected void OnPeerHangup(int id)
        {
            if (PeerHangup != null)
                PeerHangup(this, id);
        }
        protected void OnMessageFromPeer(int peer_id, string message)
        {
            if (MessageFromPeer != null)
                MessageFromPeer(this, (peer_id, message));
        }
        protected void OnServerConnectionFailure()
        {
            if (ServerConnectionFailure != null)
                ServerConnectionFailure(this, null);
        }

        /// <summary>
        /// The connection state.
        /// </summary>
        public enum State
        {
            NotConnected,
            Resolving,
            SigningIn,
            Connected,
            SigningOutWaiting,
            SigningOut
        }
        private State _state;

        private Uri _baseHttpAddress;
        private int _myId;
        private string _client_name;
        private Dictionary<int, string> _peers = new Dictionary<int, string>();

        /// <summary>
        /// Creates an instance of a Signaller.
        /// </summary>
        public Signaler()
        {
            _state = State.NotConnected;
            _myId = -1;
        }

        /// <summary>
        /// Checks if connected to the server.
        /// </summary>
        /// <returns>True if connected to the server.</returns>
        public bool IsConnected()
        {
            return _myId != -1;
        }

        /// <summary>
        /// Connects to the server.
        /// </summary>
        /// <param name="server">Host name/IP.</param>
        /// <param name="port">Port to connect.</param>
        /// <param name="client_name">Client name.</param>
        public async void Connect(string server, int port, string client_name)
        {
            try
            {
                if (_state != State.NotConnected)
                {
                    OnServerConnectionFailure();
                    return;
                }

                _client_name = client_name;
                _baseHttpAddress = new Uri("http://" + server + ":" + port);

                _state = State.SigningIn;
                await SendSignInRequestAsync();

                if (_state == State.Connected)
                {
                    var task = SendWaitRequestAsync();
                }
                else
                {
                    _state = State.NotConnected;
                    OnServerConnectionFailure();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Error] Signaling: Failed to connect to server: " + ex.Message);
            }
        }

        /// <summary>
        /// Sends "sign_in" request to the server and waits for response.
        /// </summary>
        /// <returns>False if there is a failure, otherwise returns true.</returns>
        private async Task<bool> SendSignInRequestAsync()
        {
            using (var client = new HttpClient())
            {
                string request = string.Format("sign_in?" + _client_name);

                // Send the request
                Task<string> getStringTask = client.GetStringAsync(_baseHttpAddress + request);

                // Get content from server response
                string urlContents = await getStringTask;
                if (urlContents == null)
                    return false;

                string sub = urlContents.Substring(urlContents.IndexOf(",") + 1);
                int peer_id = sub.Substring(0, sub.IndexOf(",")).ParseLeadingInt();
                int content_length = urlContents.Length;

                if (_myId == -1)
                {
                    Debug.Assert(_state == State.SigningIn);
                    _myId = peer_id;
                    Debug.Assert(_myId != -1);

                    // List of already connected peers
                    if (content_length > 0)
                    {
                        int id = 0;
                        string name = "";
                        bool connected = false;

                        if (id != _myId)
                        {
                            ParseEntry(urlContents, ref name, ref id, ref connected);
                            _peers[id] = name;
                            OnPeerConnected(id, name);
                        }
                        OnSignedIn();
                    }
                }
                if (_state == State.SigningOut)
                {
                    Close();
                    OnDisconnected();
                }
                else if (_state == State.SigningIn)
                {
                    _state = State.Connected;
                    // Send(1);
                }
            }
            return true;
        }

        /// <summary>
        /// Long lasting loop to get notified about connected/disconnected peers.
        /// </summary>
        private async Task SendWaitRequestAsync()
        {
            while (_state != State.NotConnected)
            {
                using (var client = new HttpClient())
                {
                    string request = string.Format("wait?peer_id=" + _myId);

                    client.Timeout = TimeSpan.FromMilliseconds(100);
                    var cts = new CancellationTokenSource();
                    try
                    {
                        // Send the request
                        HttpResponseMessage response = await client.GetAsync(_baseHttpAddress + request, cts.Token);
                        HttpStatusCode status_code = response.StatusCode;

                        string result;
                        if (response.IsSuccessStatusCode)
                        {
                            result = await response.Content.ReadAsStringAsync();

                            int peer_id;
                            if (!ParseServerResponse(result, status_code, out peer_id))
                                continue;

                            if (_myId == peer_id)
                            {
                                // A notification  about a new member or 
                                // a member that just disconnected 
                                int id = 0;
                                string name = "";
                                bool connected = false;
                                if (ParseEntry(result, ref name, ref id, ref connected))
                                {
                                    if (connected)
                                    {
                                        _peers[id] = name;
                                        OnPeerConnected(id, name);
                                    }
                                    else
                                    {
                                        _peers.Remove(id);
                                        OnPeerDisconnected(id);
                                    }
                                }
                            }
                            else
                            {
                                if (response.ToString().Contains("BYE"))
                                {
                                    OnPeerHangup(peer_id);
                                }
                                else
                                {
                                    Debug.WriteLine("OnMessageFromPeer!");
                                    // TODO: OnMessageFromPeer(peer_id, message);
                                    return;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[Error] Signaling SendWaitRequestAsync, Message: " + ex.Message);
                    }
                    await Task.Delay(20000);
                }
            }
        }

        /// <summary>
        /// Parses the given entry information (peer).
        /// </summary>
        /// <param name="entry">Entry info.</param>
        /// <param name="name">Peer name.</param>
        /// <param name="id">Peer ID.</param>
        /// <param name="connected">Connected status of the entry (peer).</param>
        /// <returns>False if fails to parse the entry information.</returns>
        private static bool ParseEntry(string entry, ref string name, ref int id, ref bool connected)
        {
            connected = false;

            int separator1 = entry.IndexOf(",");
            string sub = entry.Substring(separator1 + 1);
            int separator2 = sub.IndexOf(",");

            name = entry.Substring(0, separator1);
            id = sub.Substring(0, separator2).ParseLeadingInt();
            connected = sub.Substring(separator2 + 1).ParseLeadingInt() > 0 ? true : false;

            return name.Length > 0;
        }

        private bool ParseServerResponse(string content, HttpStatusCode status_code, out int peer_id)
        {
            Debug.WriteLine("ParseServerResponse!");
            peer_id = -1;
            try
            {
                if (status_code != HttpStatusCode.OK)
                {
                    if (status_code == HttpStatusCode.InternalServerError)
                    {
                        Debug.WriteLine("[Error] Signaling ParseServerResponse: " + status_code);
                        OnPeerDisconnected(0);
                        return false;
                    }
                    Close();
                    _myId = -1;
                    return false;
                }
                int separator1 = content.IndexOf(",");
                string sub = content.Substring(separator1 + 1);
                int separator2 = sub.IndexOf(",");

                peer_id = sub.Substring(0, separator2).ParseLeadingInt();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Error] Failed to parse server response (ex=" + ex.Message + ")! Buffer(" + content.Length + ")=<" + content + ">");
                return false;
            }
        }

        public async void Send(int peer_id)
        {
            await SendToPeer(peer_id, "msg!");
        }

        private async Task<bool> SendToPeer(int peer_id, string message)
        {
            try
            {
                if (_state != State.Connected)
                    return false;
                Debug.Assert(IsConnected());

                if (!IsConnected() || peer_id == -1)
                    return false;

                using (var client = new HttpClient())
                {
                    string request =
                        string.Format(
                            "message?peer_id={0}&to={1} HTTP/1.0\r\n" +
                            "Content-Length: {2}\r\n" +
                            "Content-Type: text/plain\r\n" +
                            "\r\n" +
                            "{3}",
                            _myId, peer_id, message.Length, message);

                    var content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("message", message)
                    });

                    // Send request, await response
                    HttpResponseMessage response = await client.PostAsync(
                        _baseHttpAddress + request, content);

                    if (response.StatusCode != HttpStatusCode.OK)
                        return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Error] Signaling SendToPeer: " + ex.Message);
            }
            return true;
        }

        /// <summary>
        /// Disconnects the user from the server.
        /// </summary>
        /// <returns>True if the user is disconnected from the server.</returns>
        public async Task<bool> SignOut()
        {
            if (_state == State.NotConnected || _state == State.SigningOut)
                return true;

            _state = State.SigningOut;

            if (_myId != -1)
            {
                using (var client = new HttpClient())
                {
                    string request = string.Format("sign_out?peer_id={0}", _myId);

                    // Send request, await response
                    HttpResponseMessage response = await client.GetAsync(
                        _baseHttpAddress + request);
                }
            }
            else
            {
                // Can occur if the app is closed before we finish connecting
                return true;
            }
            _myId = -1;
            _state = State.NotConnected;
            return true;
        }

        private void Close()
        {
            _peers.Clear();
            _state = State.NotConnected;
        }
    }

    public static class Extensions
    {
        public static int ParseLeadingInt(this string str)
        {
            return int.Parse(Regex.Match(str, "\\d+").Value);
        }
    }
}
