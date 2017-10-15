using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataChannelOrtc.Signaling
{
    abstract class Signaler
    {
        internal event EventHandler Connected;
        internal event EventHandler ConnectionFailed;
        internal event EventHandler Disconnected;

        internal event EventHandler<Peer> PeerConnected;
        internal event EventHandler<Peer> PeerDisconnected;
        internal event EventHandler<Peer> PeerHangup;
        internal event EventHandler<Peer> PeerMessage;

        public abstract Task Connect();

        public abstract Task<bool> SendToPeer(int id, string message);

        protected void OnConnected()
        {
            if (Connected != null)
                Connected(this, EventArgs.Empty);
        }

        protected void OnConnectionFailed()
        {
            if (ConnectionFailed != null)
                ConnectionFailed(this, EventArgs.Empty);
        }

        protected void OnDisconnected()
        {
            if (Disconnected != null)
                Disconnected(this, EventArgs.Empty);
        }

        protected void OnPeerConnected(Peer peer)
        {
            if (PeerConnected != null)
                PeerConnected(this, peer);
        }

        protected void OnPeerDisconnected(Peer peer)
        {
            if (PeerDisconnected != null)
                PeerDisconnected(this, peer);
        }

        protected void OnPeerHangup(Peer peer)
        {
            if (PeerHangup != null)
                PeerHangup(this, peer);
        }

        protected void OnPeerMessage(Peer peer)
        {
            if (PeerMessage != null)
                PeerMessage(this, peer);
        }
    }
}
