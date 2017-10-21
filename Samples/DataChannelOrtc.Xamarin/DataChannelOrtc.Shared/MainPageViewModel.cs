using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Xamarin.Forms;
//using Org.Ortc;
using DataChannelOrtc.Shared.Signaling;
using System.Diagnostics;
using System.Threading.Tasks;

namespace DataChannelOrtc.Shared
{
    public class MainPageViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<Peer> Peers { get; private set; } = new ObservableCollection<Peer>();

        public ICommand ConnectToPeerCommand { get; private set; }
        public event PropertyChangedEventHandler PropertyChanged;

        //RTCIceGatherer _gatherer;
        //RTCIceTransport _ice;           // Ice transport for the currently selected peer.
        //RTCDtlsTransport _dtls;
        //RTCSctpTransport _sctp;
        //RTCDataChannel _dataChannel;    // Data channel for the currently selected peer.
        //bool _isInitiator = false;      // True for the client that started the connection.

        //RTCDataChannelParameters _dataChannelParams = new RTCDataChannelParameters
        //{
        //    Label = "channel1",
        //    Negotiated = false,
        //    Ordered = true,
        //    Protocol = "ship"
        //};

        Signaler _signaler;

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

        public MainPageViewModel()
        {
            ConnectToPeerCommand = new Command<Peer>((peer) => ConnectToPeer(peer), (peer) => { return peer != null; });

            Peers.Add(new Peer(1, "one"));
            Peers.Add(new Peer(2, "two"));

            //OrtcLib.Setup();
            //Settings.ApplyDefaults();

            string name = Util.GetLocalPeerName();
            _signaler = new Signaler();
            _signaler.SignedIn += (sender, e) => { Debug.WriteLine("Signaller: Signed in to server."); };
            _signaler.Disconnected += (sender, e) => { Debug.WriteLine("Signaller: Disconnected from server."); };
            _signaler.PeerConnected += Signaller_PeerConnected;
            _signaler.MessageFromPeer += Signaller_MessageFromPeer;

            Task.Run(() =>
            {
                _signaler.Connect("your.signaling.server.ip", 8888, name);
            });
        }

        public void ConnectToPeer(Peer peer)
        {
            throw new NotImplementedException();
        }

        void Signaller_PeerConnected(object sender, (int id, string name) e)
        {

        }

        void Signaller_MessageFromPeer(object sender, (int peerId, string message) e)
        {

        }

        protected void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
