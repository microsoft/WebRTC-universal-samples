using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Org.Ortc;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using DataChannelOrtc.Signaling;
using Org.Ortc.Log;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace DataChannelOrtc
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        RTCIceGatherer _gatherer;
        RTCIceTransport _ice;           // Ice transport for the currently selected peer.
        RTCDtlsTransport _dtls;
        RTCSctpTransport _sctp;
        RTCDataChannel _dataChannel;    // Data channel for the currently selected peer.
        bool _isInitiator = false;      // True for the client that started the connection.

        RTCDataChannelParameters _dataChannelParams = new RTCDataChannelParameters
        {
            Label = "channel1",
            Negotiated = false,
            Ordered = true,
            Protocol = "ship"
        };

        Signaler _signaler;

        public ObservableCollection<Peer> Peers = new ObservableCollection<Peer>();

        public event PropertyChangedEventHandler PropertyChanged;

        private Peer _remotePeer;
        public Peer RemotePeer
        {
            get
            {
                if (_remotePeer == null)
                    _remotePeer = SelectedPeer;
                return _remotePeer;
            }
            set
            {
                if (_remotePeer == value)
                    return;
                _remotePeer = value;
            }
        }

        private Peer _selectedPeer;
        public Peer SelectedPeer
        {
            get { return _selectedPeer; }
            set
            {
                if (_selectedPeer == value)
                    return;

                var oldValue = _selectedPeer;
                _selectedPeer = value;
                OnPropertyChanged(nameof(SelectedPeer));

                SelectedPeerChanged(oldValue, value);
            }
        }

        private string _message = string.Empty;
        public string Message
        {
            get { return _message; }
            set
            {
                if (_message == value)
                    return;

                _message = value;
                OnPropertyChanged(nameof(Message));
            }
        }

        private string _conversation = string.Empty;
        public string Conversation
        {
            get { return _conversation; }
            set
            {
                if (_conversation == value)
                    return;

                _conversation = value;
                OnPropertyChanged(nameof(Conversation));
            }
        }

        private bool _isSendEnabled = false;
        public bool IsSendEnabled
        {
            get { return _isSendEnabled; }
            set
            {
                if (_isSendEnabled == value)
                    return;

                _isSendEnabled = value;
                OnPropertyChanged(nameof(IsSendEnabled));
            }
        }

        public MainPage()
        {
            this.InitializeComponent();

            this.Loaded += MainPage_Loaded;
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            Ortc.Setup();
            Settings.ApplyDefaults();
            Logger.InstallTelnetLogger(59999, 60, true);
            Logger.SetLogLevel(Level.Trace);

            var name = GetLocalPeerName();
            Debug.WriteLine($"Connecting to server from local peer: {name}");

            _signaler = new TcpSignaler("40.83.179.150", "8888", name);

            _signaler.Connected += Signaler_Connected;
            _signaler.ConnectionFailed += Signaler_ConnectionFailed;
            _signaler.PeerConnected += Signaler_PeerConnected;
            _signaler.PeerMessage += Signaler_MessageFromPeer;

            // It's important to run the below asynchronously for now as it blocks the current thread while waiting for socket data.
            Task.Run(() =>
            {
                _signaler.Connect();
            });

        }

        private void Signaler_Connected(object sender, EventArgs e)
        {
            Debug.WriteLine("Signaler connected to server.");
        }

        private void Signaler_ConnectionFailed(object sender, EventArgs e)
        {
            Debug.WriteLine("Server connection failure.");
        }

        private void SelectedPeerChanged(Peer oldPeer, Peer peer)
        {
            // When the selected peer changes tear down the old datachannel and prepare for a new connection.
            if (_dataChannel != null)
            {
                _dataChannel.Close();
                _sctp.Stop();
                _dtls.Stop();
                _ice.Stop();
            }


            Conversation = string.Empty;
            Message = string.Empty;
            IsSendEnabled = false;

            InitializeORTC();
        }

        private async Task InitializeORTC()
        {
            var gatherOptions = new RTCIceGatherOptions()
            {
                IceServers = new List<RTCIceServer>()
                {
                    new RTCIceServer { Urls = new string[] { "stun.l.google.com:19302" }  },
                    new RTCIceServer { Username = "redmond", Credential = "redmond123", CredentialType = RTCIceGathererCredentialType.Password, Urls = new string[] { "turn:turn-testdrive.cloudapp.net:3478?transport=udp" } }
                }
            };

            _gatherer = new RTCIceGatherer(gatherOptions);
            _gatherer.OnStateChange += IceGatherer_OnStateChange;

            _gatherer.OnLocalCandidate += async (candidate) =>
            {
                await _signaler.SendToPeer(RemotePeer.Id, candidate.Candidate.ToJsonString());
            };

            var cert = await RTCCertificate.GenerateCertificate();

            _ice = new RTCIceTransport(_gatherer);
            _ice.OnStateChange += IceTransport_OnStateChange;

            _dtls = new RTCDtlsTransport(_ice, new RTCCertificate[] { cert });
            _dtls.OnStateChange += Dtls_OnStateChange;

            _sctp = new RTCSctpTransport(_dtls);
        }

        private async void Signaler_PeerConnected(object sender, Peer peer)
        {
            Debug.WriteLine($"Peer connected {peer.Name} / {peer.Id}");

            Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                Peers.Add(peer);
                if (SelectedPeer == null)
                    SelectedPeer = peer;
            });
        }

        private void Signaler_MessageFromPeer(object sender, Peer peer)
        {
            var message = peer.Message;

            if (message.Contains("OpenDataChannel"))
            {
                // A peer has let us know that they have begun initiating a data channel.  In this scenario,
                // we are the "remote" peer so make sure _isInitiator is false.  Begin gathering ice candidates.
                _isInitiator = false;
                RemotePeer = peer;
                OpenDataChannel(peer);    // TODO: This is incorrect - message is definitely not the peer's name.
                return;
            }

            if (message.Contains("IceCandidate"))
            {
                var remoteCandidate = RTCIceCandidate.FromJsonString(message);
                _ice.AddRemoteCandidate(remoteCandidate);

                // TODO: Notify the ice transport when gathering is complete via the line below so it can change state.
                //_ice.AddRemoteCandidate(new RTCIceCandidateComplete());
                return;
            }

            if (message.Contains("IceParameters"))
            {
                var iceParameters = RTCIceParameters.FromJsonString(message);
                // Start the ice transport with the appropriate role based on whether this is the initiator of the call.
                var role = _isInitiator ? RTCIceRole.Controlling : RTCIceRole.Controlled;
                _ice.Start(_gatherer, iceParameters, role);
                return;
            }

            if (message.Contains("DtlsParameters"))
            {
                var dtlsParameters = RTCDtlsParameters.FromJsonString(message);
                _dtls.Start(dtlsParameters);
                Debug.WriteLine("Dtls start called.");
                return;
            }

            // this order guarantees: 
            if (message.Contains("SctpCapabilities"))
            {
                // Message ordering: alice -> bob; bob.start(); bob -> alice; alice.start(); alice -> datachannel -> bob
                var sctpCaps = RTCSctpCapabilities.FromJsonString(message);

                if (!_isInitiator)
                {
                    Debug.WriteLine("Receiver: Waiting for OnDataChannel event and starting sctp.");

                    // The remote side will receive notification when the data channel is opened.
                    _sctp.OnDataChannel += Sctp_OnDataChannel;
                    _sctp.OnStateChange += Sctp_OnStateChange;

                    _sctp.Start(sctpCaps);

                    var caps = RTCSctpTransport.GetCapabilities();
                    _signaler.SendToPeer(RemotePeer.Id, caps.ToJsonString());
                }
                else
                {
                    // The initiator has received sctp caps back from the remote peer, which means the remote peer
                    // has already called sctp.start().  It's now safe to open a data channel, which will fire the
                    // Sctp_OnDataChannel event on the remote side.
                    Debug.WriteLine("Initiator: Creating the data channel and starting sctp.");

                    _sctp.OnStateChange += Sctp_OnStateChange;
                    _sctp.Start(sctpCaps);
                    _dataChannel = new RTCDataChannel(_sctp, _dataChannelParams);
                    _dataChannel.OnMessage += DataChannel_OnMessage;
                    _dataChannel.OnError += DataChannel_OnError;
                    _dataChannel.OnStateChange += DataChannel_OnStateChange;

                    Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        IsSendEnabled = true;
                    });
                }
            }
        }

        private void DataChannel_OnError(ErrorEvent evt)
        {
            Debug.WriteLine("DataChannel error: " + evt.Error);
        }

        private void DataChannel_OnMessage(RTCMessageEvent evt)
        {
            Debug.WriteLine("DataChannel message: " + evt.Data.Text);

            AppendConversation(evt.Data.Text, false);
        }

        private void DataChannel_OnStateChange(RTCDataChannelStateChangeEvent evt)
        {
            Debug.WriteLine("DataChannel sate: " + evt.State);
        }

        private void AppendConversation(string text, bool isLocal)
        {
            Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                Conversation += Environment.NewLine + (isLocal ? "Local " : "Remote ") + DateTime.Now.ToString() + ": " + text;
            });
        }

        /// <summary>
        /// Estabilishes a DataChannel with the parameter peer.
        /// </summary>
        private async Task OpenDataChannel(Peer peer)
        {
            Debug.WriteLine($"Opening data channel to peer: {peer.Name}");

            Conversation = string.Empty;

            Debug.WriteLine("Ice: Gathering candidates.");
            _gatherer.Gather(null);

            var iceParams = _gatherer.GetLocalParameters();
            await _signaler.SendToPeer(peer.Id, iceParams.ToJsonString());

            // this order guarantees: alice -> bob; bob.start(); bob -> alice; alice.start(); alice -> datachannel -> bob
            if (_isInitiator)
            {
                var sctpCaps = RTCSctpTransport.GetCapabilities();
                await _signaler.SendToPeer(peer.Id, sctpCaps.ToJsonString());
            }

            var dtlsParams = _dtls.GetLocalParameters();
            await _signaler.SendToPeer(peer.Id, dtlsParams.ToJsonString());
        }

        private void Dtls_OnStateChange(RTCDtlsTransportStateChangedEvent evt)
        {
            Debug.WriteLine("Dtls State Change: " + evt.State);
        }

        private void IceTransport_OnStateChange(RTCIceTransportStateChangedEvent evt)
        {
            Debug.WriteLine("IceTransport State Change: " + evt.State);
        }

        private void IceGatherer_OnStateChange(RTCIceGathererStateChangedEvent evt)
        {
            Debug.WriteLine("IceGatherer State Change: " + evt.State);
        }

        private void Sctp_OnDataChannel(RTCDataChannelEvent evt)
        {
            Debug.WriteLine("Sctp OnDataChannel");
            _dataChannel = evt.DataChannel;
            _dataChannel.OnMessage += DataChannel_OnMessage;
            _dataChannel.OnError += DataChannel_OnError;
            _dataChannel.OnStateChange += DataChannel_OnStateChange;

            // Data channel is now open and ready for use.  This will fire on the receiver side of the call; send
            // a message back to the initiator.
            //_dataChannel.Send("Hello data channel!");

            Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                IsSendEnabled = true;
            });
        }

        private void Sctp_OnStateChange(RTCSctpTransportStateChangeEvent evt)
        {
            Debug.WriteLine("Sctp State Change: " + evt.State);
        }

        private void uxSend_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(Message))
                return;

            AppendConversation(Message, true);
            _dataChannel.Send(Message);
            Message = string.Empty;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Constructs and returns the local peer name.
        /// </summary>
        /// <returns>The local peer name.</returns>
        private string GetLocalPeerName()
        {
            var hostname = NetworkInformation.GetHostNames().FirstOrDefault(h => h.Type == HostNameType.DomainName);
            string ret = hostname?.CanonicalName ?? "<unknown host>";
            ret = ret + "-" + ((DateTime.Now.Ticks - 621355968000000000) / 10000000).ToString();
            ret = ret + "-dual";
            return ret;
        }

        private async void uxConnectToPeer_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPeer == null)
                return;

            _isInitiator = true;

            // Signal to the peer that we're going to open a connection.  This gives the peer the opportunity to
            // begin gathering ice candidates immediately.  On the remote peer side, _isInitiator will remain false.

            RemotePeer = SelectedPeer;

            await _signaler.SendToPeer(RemotePeer.Id, "OpenDataChannel");

            OpenDataChannel(SelectedPeer);
        }
    }
}