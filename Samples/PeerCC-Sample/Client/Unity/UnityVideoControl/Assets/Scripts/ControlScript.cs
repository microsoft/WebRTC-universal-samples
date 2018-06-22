using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

#if !UNITY_EDITOR
using Org.WebRtc;
using Windows.Media.Core;
#endif

public class ControlScript : MonoBehaviour
{
    public uint LocalTextureWidth = 160;
    public uint LocalTextureHeight = 120;
    public uint RemoteTextureWidth = 640;
    public uint RemoteTextureHeight = 480;
    
    public RawImage LocalVideoImage;
    public RawImage RemoteVideoImage;

	void Awake()
    {
    }
    
    void Start()
    {
	}

    private void OnInitialized()
    {
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
    }

    public void CreateLocalMediaStreamSource(object track, string type, string id)
    {
#if !UNITY_EDITOR
        MediaVideoTrack videoTrack = (MediaVideoTrack)track;
        var source = Media.CreateMedia().CreateMediaStreamSource(videoTrack, type, id);
        Plugin.LoadLocalMediaStreamSource((MediaStreamSource)source);
#endif
    }

    public void DestroyLocalMediaStreamSource()
    {
        Plugin.UnloadLocalMediaStreamSource();
    }

    public void CreateRemoteMediaStreamSource(object track, string type, string id)
    {
#if !UNITY_EDITOR
        MediaVideoTrack videoTrack = (MediaVideoTrack)track;
        var source = Media.CreateMedia().CreateMediaStreamSource(videoTrack, type, id);
        Plugin.LoadRemoteMediaStreamSource((MediaStreamSource)source);
#endif
    }

    public void DestroyRemoteMediaStreamSource()
    {
        Plugin.UnloadRemoteMediaStreamSource();
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
