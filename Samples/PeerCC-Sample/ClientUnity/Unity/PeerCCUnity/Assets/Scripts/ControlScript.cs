using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.EventSystems;

#if !UNITY_EDITOR
using Windows.UI.Core;
using Windows.Foundation;
using Windows.Media.Core;
using System.Linq;
using System.Threading.Tasks;
using PeerConnectionClient.Signalling;
using Windows.ApplicationModel.Core;
#endif

public class ControlScript : MonoBehaviour
{
    public static ControlScript Instance { get; private set; }

    public uint LocalTextureWidth = 160;
    public uint LocalTextureHeight = 120;
    public uint RemoteTextureWidth = 640;
    public uint RemoteTextureHeight = 480;
    
    public RawImage LocalVideoImage;
    public RawImage RemoteVideoImage;

    public InputField ServerAddressInputField;
    public Button ConnectButton;
    public Button CallButton;
    public RectTransform PeerContent;
    public GameObject TextItemPreftab;

    private enum Status
    {
        NotConnected,
        Connecting,
        Disconnecting,
        Connected,
        Calling,
        EndingCall,
        InCall
    }

    private enum CommandType
    {
        Empty,
        SetNotConnected,
        SetConnected,
        SetInCall,
        AddRemotePeer,
        RemoveRemotePeer
    }

    private struct Command
    {
        public CommandType type;
#if !UNITY_EDITOR
        public Conductor.Peer remotePeer;
#endif
    }

    private Status status = Status.NotConnected;
    private List<Command> commandQueue = new List<Command>();
    private int selectedPeerIndex = -1;

    public ControlScript()
    {
    }

    void Awake()
    {
    }
    
    void Start()
    {
        Instance = this;

#if !UNITY_EDITOR
        Conductor.Instance.Initialized += Conductor_Initialized;
        Conductor.Instance.Initialize(CoreApplication.MainView.CoreWindow.Dispatcher);
        Conductor.Instance.EnableLogging(Conductor.LogLevel.Verbose);
#endif
        ServerAddressInputField.text = "peercc-server.ortclib.org";
    }

    private void OnEnable()
    {
        {
            Plugin.CreateLocalMediaPlayback();
            IntPtr nativeTex = IntPtr.Zero;
            Plugin.GetLocalPrimaryTexture(LocalTextureWidth, LocalTextureHeight, out nativeTex);
            var primaryPlaybackTexture = Texture2D.CreateExternalTexture((int)LocalTextureWidth, (int)LocalTextureHeight, TextureFormat.BGRA32, false, false, nativeTex);
            LocalVideoImage.texture = primaryPlaybackTexture;
        }

        {
            Plugin.CreateRemoteMediaPlayback();
            IntPtr nativeTex = IntPtr.Zero;
            Plugin.GetRemotePrimaryTexture(RemoteTextureWidth, RemoteTextureHeight, out nativeTex);
            var primaryPlaybackTexture = Texture2D.CreateExternalTexture((int)RemoteTextureWidth, (int)RemoteTextureHeight, TextureFormat.BGRA32, false, false, nativeTex);
            RemoteVideoImage.texture = primaryPlaybackTexture;
        }
    }

    private void OnDisable()
    {
        LocalVideoImage.texture = null;
        Plugin.ReleaseLocalMediaPlayback();
        RemoteVideoImage.texture = null;
        Plugin.ReleaseRemoteMediaPlayback();
    }

    private void Update()
    {
#if !UNITY_EDITOR
        lock (this)
        {
            switch (status)
            {
                case Status.NotConnected:
                    if (!ServerAddressInputField.enabled)
                        ServerAddressInputField.enabled = true;
                    if (!ConnectButton.enabled)
                        ConnectButton.enabled = true;
                    if (CallButton.enabled)
                        CallButton.enabled = false;
                    break;
                case Status.Connecting:
                    if (ServerAddressInputField.enabled)
                        ServerAddressInputField.enabled = false;
                    if (ConnectButton.enabled)
                        ConnectButton.enabled = false;
                    if (CallButton.enabled)
                        CallButton.enabled = false;
                    break;
                case Status.Disconnecting:
                    if (ServerAddressInputField.enabled)
                        ServerAddressInputField.enabled = false;
                    if (ConnectButton.enabled)
                        ConnectButton.enabled = false;
                    if (CallButton.enabled)
                        CallButton.enabled = false;
                    break;
                case Status.Connected:
                    if (ServerAddressInputField.enabled)
                        ServerAddressInputField.enabled = false;
                    if (!ConnectButton.enabled)
                        ConnectButton.enabled = true;
                    if (!CallButton.enabled)
                        CallButton.enabled = true;
                    break;
                case Status.Calling:
                    if (ServerAddressInputField.enabled)
                        ServerAddressInputField.enabled = false;
                    if (ConnectButton.enabled)
                        ConnectButton.enabled = false;
                    if (CallButton.enabled)
                        CallButton.enabled = false;
                    break;
                case Status.EndingCall:
                    if (ServerAddressInputField.enabled)
                        ServerAddressInputField.enabled = false;
                    if (ConnectButton.enabled)
                        ConnectButton.enabled = false;
                    if (CallButton.enabled)
                        CallButton.enabled = false;
                    break;
                case Status.InCall:
                    if (ServerAddressInputField.enabled)
                        ServerAddressInputField.enabled = false;
                    if (ConnectButton.enabled)
                        ConnectButton.enabled = false;
                    if (!CallButton.enabled)
                        CallButton.enabled = true;
                    break;
                default:
                    break;
            }

            while (commandQueue.Count != 0)
            {
                Command command = commandQueue.First();
                commandQueue.RemoveAt(0);
                switch (status)
                {
                    case Status.NotConnected:
                        if (command.type == CommandType.SetNotConnected)
                        {
                            ConnectButton.GetComponentInChildren<Text>().text = "Connect";
                            CallButton.GetComponentInChildren<Text>().text = "Call";
                        }
                        break;
                    case Status.Connected:
                        if (command.type == CommandType.SetConnected)
                        {
                            ConnectButton.GetComponentInChildren<Text>().text = "Disconnect";
                            CallButton.GetComponentInChildren<Text>().text = "Call";
                        }
                        break;
                    case Status.InCall:
                        if (command.type == CommandType.SetInCall)
                        {
                            ConnectButton.GetComponentInChildren<Text>().text = "Disconnect";
                            CallButton.GetComponentInChildren<Text>().text = "Hang Up";
                        }
                        break;
                    default:
                        break;
                }
                if (command.type == CommandType.AddRemotePeer)
                {
                    GameObject textItem = (GameObject)Instantiate(TextItemPreftab);
                    textItem.transform.SetParent(PeerContent);
                    textItem.GetComponent<Text>().text = command.remotePeer.Name;
                    EventTrigger trigger = textItem.GetComponentInChildren<EventTrigger>();
                    EventTrigger.Entry entry = new EventTrigger.Entry();
                    entry.eventID = EventTriggerType.PointerDown;
                    entry.callback.AddListener((data) => { OnRemotePeerItemClick((PointerEventData)data); });
                    trigger.triggers.Add(entry);
                    if (selectedPeerIndex == -1)
                    {
                        textItem.GetComponent<Text>().fontStyle = FontStyle.Bold;
                        selectedPeerIndex = PeerContent.transform.childCount - 1;
                    }
                }
                else if (command.type == CommandType.RemoveRemotePeer)
                {
                    for (int i = 0; i < PeerContent.transform.childCount; i++)
                    {
                        if (PeerContent.GetChild(i).GetComponent<Text>().text == command.remotePeer.Name)
                        {
                            PeerContent.GetChild(i).SetParent(null);
                            if (selectedPeerIndex == i)
                            {
                                if (PeerContent.transform.childCount > 0)
                                {
                                    PeerContent.GetChild(0).GetComponent<Text>().fontStyle = FontStyle.Bold;
                                    selectedPeerIndex = 0;
                                }
                                else
                                {
                                    selectedPeerIndex = -1;
                                }
                            }
                            break;
                        }
                    }
                }
            }
        }
#endif
    }

    private void Conductor_Initialized(bool succeeded)
    {
        if (succeeded)
        {
            Initialize();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("Conductor initialization failed");
        }
    }

    public void OnConnectClick()
    {
#if !UNITY_EDITOR
        lock (this)
        {
            if (status == Status.NotConnected)
            {
                new Task(() =>
                {
                    Conductor.Instance.StartLogin(ServerAddressInputField.text, "8888");
                }).Start();
                status = Status.Connecting;
            }
            else if (status == Status.Connected)
            {
                new Task(() =>
                {
                    var task = Conductor.Instance.DisconnectFromServer();
                }).Start();

                status = Status.Disconnecting;
                selectedPeerIndex = -1;
                PeerContent.DetachChildren();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("OnConnectClick() - wrong status - " + status);
            }
        }
#endif
    }

    public void OnCallClick()
    {
#if !UNITY_EDITOR
        lock (this)
        {
            if (status == Status.Connected)
            {
                if (selectedPeerIndex == -1)
                    return;
                new Task(() =>
                {
                    Conductor.Peer conductorPeer = Conductor.Instance.GetPeers()[selectedPeerIndex];
                    if (conductorPeer != null)
                    {
                        Conductor.Instance.ConnectToPeer(conductorPeer);
                    }
                }).Start();
                status = Status.Calling;
            }
            else if (status == Status.InCall)
            {
                new Task(() =>
                {
                    var task = Conductor.Instance.DisconnectFromPeer();
                }).Start();
                status = Status.EndingCall;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("OnCallClick() - wrong status - " + status);
            }
        }
#endif
    }

    public void OnRemotePeerItemClick(PointerEventData data)
    {
#if !UNITY_EDITOR
        for (int i = 0; i < PeerContent.transform.childCount; i++)
        {
            if (PeerContent.GetChild(i) == data.selectedObject.transform)
            {
                data.selectedObject.GetComponent<Text>().fontStyle = FontStyle.Bold;
                selectedPeerIndex = i;
            }
            else
            {
                PeerContent.GetChild(i).GetComponent<Text>().fontStyle = FontStyle.Normal;
            }
        }
#endif
    }

#if !UNITY_EDITOR
    public async Task OnAppSuspending()
    {
        Conductor.Instance.CancelConnectingToPeer();

        await Conductor.Instance.DisconnectFromPeer();
        await Conductor.Instance.DisconnectFromServer();

        Conductor.Instance.OnAppSuspending();
    }

    private IAsyncAction RunOnUiThread(Action fn)
    {
        return CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(fn));
    }
#endif

    public void Initialize()
    {
#if !UNITY_EDITOR
        // A Peer is connected to the server event handler
        Conductor.Instance.Signaller.OnPeerConnected += (peerId, peerName) =>
        {
            var task = RunOnUiThread(() =>
            {
                lock (this)
                {
                    Conductor.Peer peer = new Conductor.Peer { Id = peerId, Name = peerName };
                    Conductor.Instance.AddPeer(peer);
                    commandQueue.Add(new Command { type = CommandType.AddRemotePeer, remotePeer = peer });
                }
            });
        };

        // A Peer is disconnected from the server event handler
        Conductor.Instance.Signaller.OnPeerDisconnected += peerId =>
        {
            var task = RunOnUiThread(() =>
            {
                lock (this)
                {
                    var peerToRemove = Conductor.Instance.GetPeers().FirstOrDefault(p => p.Id == peerId);
                    if (peerToRemove != null)
                    {
                        Conductor.Peer peer = new Conductor.Peer { Id = peerToRemove.Id, Name = peerToRemove.Name };
                        Conductor.Instance.RemovePeer(peer);
                        commandQueue.Add(new Command { type = CommandType.RemoveRemotePeer, remotePeer = peer });
                    }
                }
            });
        };

        // The user is Signed in to the server event handler
        Conductor.Instance.Signaller.OnSignedIn += () =>
        {
            var task = RunOnUiThread(() =>
            {
                lock (this)
                {
                    if (status == Status.Connecting)
                    {
                        status = Status.Connected;
                        commandQueue.Add(new Command { type = CommandType.SetConnected });
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Signaller.OnSignedIn() - wrong status - " + status);
                    }
                }
            });
        };

        // Failed to connect to the server event handler
        Conductor.Instance.Signaller.OnServerConnectionFailure += () =>
        {
            var task = RunOnUiThread(() =>
            {
                lock (this)
                {
                    if (status == Status.Connecting)
                    {
                        status = Status.NotConnected;
                        commandQueue.Add(new Command { type = CommandType.SetNotConnected });
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Signaller.OnServerConnectionFailure() - wrong status - " + status);
                    }
                }
            });
        };

        // The current user is disconnected from the server event handler
        Conductor.Instance.Signaller.OnDisconnected += () =>
        {
            var task = RunOnUiThread(() =>
            {
                lock (this)
                {
                    if (status == Status.Disconnecting)
                    {
                        status = Status.NotConnected;
                        commandQueue.Add(new Command { type = CommandType.SetNotConnected });
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Signaller.OnDisconnected() - wrong status - " + status);
                    }
                }
            });
        };

        Conductor.Instance.OnAddRemoteStream += Conductor_OnAddRemoteStream;
        Conductor.Instance.OnRemoveRemoteStream += Conductor_OnRemoveRemoteStream;
        Conductor.Instance.OnAddLocalStream += Conductor_OnAddLocalStream;

        // Connected to a peer event handler
        Conductor.Instance.OnPeerConnectionCreated += () =>
        {
            var task = RunOnUiThread(() =>
            {
                lock (this)
                {
                    if (status == Status.Calling)
                    {
                        status = Status.InCall;
                        commandQueue.Add(new Command { type = CommandType.SetInCall });
                    }
                    else if (status == Status.Connected)
                    {
                        status = Status.InCall;
                        commandQueue.Add(new Command { type = CommandType.SetInCall });
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Conductor.OnPeerConnectionCreated() - wrong status - " + status);
                    }
                }
            });
        };

        // Connection between the current user and a peer is closed event handler
        Conductor.Instance.OnPeerConnectionClosed += () =>
        {
            var task = RunOnUiThread(() =>
            {
                lock (this)
                {
                    if (status == Status.EndingCall)
                    {
                        Plugin.UnloadLocalMediaStreamSource();
                        Plugin.UnloadRemoteMediaStreamSource();
                        status = Status.Connected;
                        commandQueue.Add(new Command { type = CommandType.SetConnected });
                    }
                    else if (status == Status.InCall)
                    {
                        Plugin.UnloadLocalMediaStreamSource();
                        Plugin.UnloadRemoteMediaStreamSource();
                        status = Status.Connected;
                        commandQueue.Add(new Command { type = CommandType.SetConnected });
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Conductor.OnPeerConnectionClosed() - wrong status - " + status);
                    }
                }
            });
        };

        // Ready to connect to the server event handler
        Conductor.Instance.OnReadyToConnect += () => { var task = RunOnUiThread(() => { }); };

        List<Conductor.IceServer> iceServers = new List<Conductor.IceServer>();
        iceServers.Add(new Conductor.IceServer { Host = "stun.l.google.com:19302", Type = Conductor.IceServer.ServerType.STUN });
        iceServers.Add(new Conductor.IceServer { Host = "stun1.l.google.com:19302", Type = Conductor.IceServer.ServerType.STUN });
        iceServers.Add(new Conductor.IceServer { Host = "stun2.l.google.com:19302", Type = Conductor.IceServer.ServerType.STUN });
        iceServers.Add(new Conductor.IceServer { Host = "stun3.l.google.com:19302", Type = Conductor.IceServer.ServerType.STUN });
        iceServers.Add(new Conductor.IceServer { Host = "stun4.l.google.com:19302", Type = Conductor.IceServer.ServerType.STUN });
        Conductor.IceServer turnServer = new Conductor.IceServer { Host = "turnserver3dstreaming.centralus.cloudapp.azure.com:5349", Type = Conductor.IceServer.ServerType.TURN };
        turnServer.Credential = "3Dtoolkit072017";
        turnServer.Username = "user";
        iceServers.Add(turnServer);
        Conductor.Instance.ConfigureIceServers(iceServers);

        var audioCodecList = Conductor.Instance.GetAudioCodecs();
        Conductor.Instance.AudioCodec = audioCodecList.FirstOrDefault(c => c.Name == "opus");
        System.Diagnostics.Debug.WriteLine("Selected audio codec - " + Conductor.Instance.AudioCodec.Name);

        // Order the video codecs so that the stable VP8 is in front.
        var videoCodecList = Conductor.Instance.GetVideoCodecs();
        Conductor.Instance.VideoCodec = videoCodecList.FirstOrDefault(c => c.Name == "H264");
        System.Diagnostics.Debug.WriteLine("Selected video codec - " + Conductor.Instance.VideoCodec.Name);

        uint preferredWidth = 896;
        uint preferredHeght = 504;
        uint preferredFrameRate = 15;
        uint minSizeDiff = uint.MaxValue;
        Conductor.CaptureCapability selectedCapability = null;
        var videoDeviceList = Conductor.Instance.GetVideoCaptureDevices();
        foreach (Conductor.MediaDevice device in videoDeviceList)
        {
            Conductor.Instance.GetVideoCaptureCapabilities(device.Id).AsTask().ContinueWith(capabilities =>
            {
                foreach (Conductor.CaptureCapability capability in capabilities.Result)
                {
                    uint sizeDiff = (uint)Math.Abs(preferredWidth - capability.Width) + (uint)Math.Abs(preferredHeght - capability.Height);
                    if (sizeDiff < minSizeDiff)
                    {
                        selectedCapability = capability;
                        minSizeDiff = sizeDiff;
                    }
                    System.Diagnostics.Debug.WriteLine("Video device capability - " + device.Name + " - " + capability.Width + "x" + capability.Height + "@" + capability.FrameRate);
                }
            }).Wait();
        }

        if (selectedCapability != null)
        {
            selectedCapability.FrameRate = preferredFrameRate;
            selectedCapability.MrcEnabled = true;
            Conductor.Instance.VideoCaptureProfile = selectedCapability;
            Conductor.Instance.UpdatePreferredFrameFormat();
            System.Diagnostics.Debug.WriteLine("Selected video device capability - " + selectedCapability.Width + "x" + selectedCapability.Height + "@" + selectedCapability.FrameRate);
        }

#endif
    }

    private void Conductor_OnAddRemoteStream()
    {
#if !UNITY_EDITOR
        var task = RunOnUiThread(() =>
        {
            lock (this)
            {
                if (status == Status.InCall)
                {
                    IMediaSource source;
                    if (Conductor.Instance.VideoCodec.Name == "H264")
                        source = Conductor.Instance.CreateRemoteMediaStreamSource("H264");
                    else
                        source = Conductor.Instance.CreateRemoteMediaStreamSource("I420");
                    Plugin.LoadRemoteMediaStreamSource((MediaStreamSource)source);
                }
                else if (status == Status.Connected)
                {
                    IMediaSource source;
                    if (Conductor.Instance.VideoCodec.Name == "H264")
                        source = Conductor.Instance.CreateRemoteMediaStreamSource("H264");
                    else
                        source = Conductor.Instance.CreateRemoteMediaStreamSource("I420");
                    Plugin.LoadRemoteMediaStreamSource((MediaStreamSource)source);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Conductor.OnAddRemoteStream() - wrong status - " + status);
                }
            }
        });
#endif
    }

    private void Conductor_OnRemoveRemoteStream()
    {
#if !UNITY_EDITOR
        var task = RunOnUiThread(() =>
        {
            lock (this)
            {
                if (status == Status.InCall)
                {
                }
                else if (status == Status.Connected)
                {
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Conductor.OnRemoveRemoteStream() - wrong status - " + status);
                }
            }
        });
#endif
    }

    private void Conductor_OnAddLocalStream()
    {
#if !UNITY_EDITOR
        var task = RunOnUiThread(() =>
        {
            lock (this)
            {
                if (status == Status.InCall)
                {
                    var source = Conductor.Instance.CreateLocalMediaStreamSource("I420");
                    Plugin.LoadLocalMediaStreamSource((MediaStreamSource)source);

                    Conductor.Instance.EnableLocalVideoStream();
                    Conductor.Instance.UnmuteMicrophone();
                }
                else if (status == Status.Connected)
                {
                    var source = Conductor.Instance.CreateLocalMediaStreamSource("I420");
                    Plugin.LoadLocalMediaStreamSource((MediaStreamSource)source);

                    Conductor.Instance.EnableLocalVideoStream();
                    Conductor.Instance.UnmuteMicrophone();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Conductor.OnAddLocalStream() - wrong status - " + status);
                }
            }
        });
#endif
    }

    private static class Plugin
    {
        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "CreateLocalMediaPlayback")]
        internal static extern void CreateLocalMediaPlayback();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "CreateRemoteMediaPlayback")]
        internal static extern void CreateRemoteMediaPlayback();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "ReleaseLocalMediaPlayback")]
        internal static extern void ReleaseLocalMediaPlayback();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "ReleaseRemoteMediaPlayback")]
        internal static extern void ReleaseRemoteMediaPlayback();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "GetLocalPrimaryTexture")]
        internal static extern void GetLocalPrimaryTexture(UInt32 width, UInt32 height, out System.IntPtr playbackTexture);

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "GetRemotePrimaryTexture")]
        internal static extern void GetRemotePrimaryTexture(UInt32 width, UInt32 height, out System.IntPtr playbackTexture);

#if !UNITY_EDITOR
        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "LoadLocalMediaStreamSource")]
        internal static extern void LoadLocalMediaStreamSource(MediaStreamSource IMediaSourceHandler);

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "UnloadLocalMediaStreamSource")]
        internal static extern void UnloadLocalMediaStreamSource();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "LoadRemoteMediaStreamSource")]
        internal static extern void LoadRemoteMediaStreamSource(MediaStreamSource IMediaSourceHandler);

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "UnloadRemoteMediaStreamSource")]
        internal static extern void UnloadRemoteMediaStreamSource();
#endif

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "LocalPlay")]
        internal static extern void LocalPlay();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "RemotePlay")]
        internal static extern void RemotePlay();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "LocalPause")]
        internal static extern void LocalPause();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "RemotePause")]
        internal static extern void RemotePause();
    }
}
