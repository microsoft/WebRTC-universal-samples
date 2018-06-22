# PeerConnection Sample for WebRTC (NuGet)
This repo contains the PeerConnection sample WebRTC sample for establishing an audio/video call between two peers.  It includes a pre-compiled [signalling server](../Server) and references the [WebRTC UWP NuGet package](https://www.nuget.org/packages/WebRtc/) making it the quickest way to get up and running with WebRTC on UWP.  This sample also includes Unity support and features targeted at HoloLens and Mixed-Reality development.  For the same samples with reference to the complete WebRTC UWP source instead of a NuGet package, see <https://github.com/webrtc-uwp/PeerCC>.  This is based on the original PeerConnection sample from <https://webrtc.org>.

## Samples

- [PeerConnectionClient.WebRtc.sln](../PeerConnectionClient.WebRtc.sln) **UWP PeerConnection XAML/C# Sample**
<br>Start here for non-MR/HoloLens development.
- [WebRtcUnityD3D.sln](../PeerConnectionClient.WebRtc.UnityD3D.sln) **Unity standalone PeerConnection Sample**
<br>Standalone Unity sample with full screen 3D rendering.  Most commonly used sample for Unity HoloLens projects.  Read Mike Taulty's [blog post](https://mtaulty.com/2018/03/15/rough-notes-on-uwp-and-webrtc-part-4-adding-some-unity-and-a-little-hololens/) for more detail on the solution.
- [WebRtcUnityXaml.sln](../PeerConnectionClient.WebRtc.UnityXaml.sln) **UWP PeerConnection XAML/C# with video rendered in Unity**
<br>Peer Connection Client UWP/XAML application with video rendered in a Unity scene via [SwapChainPanel](https://docs.microsoft.com/en-us/uwp/api/Windows.UI.Xaml.Controls.SwapChainPanel).  Useful if you want to render all content in a UWP window and use XAML to build most of the UI, but include some 3D content in the app.
>**Known issue:** Mixed-Reality Capture is not currently working in this sample.

## Build requirements

1. Unity version 2017.4.1 with Windows Store .NET Scripting Backend
>**Note:** If Unty is not installed on default location (C:\Program Files\Unity), edit install path values in property files common\windows\samples\PeerCC\Client\UnityCommon.props and common\windows\samples\PeerCC\ClientUnity\UnityCommon.props.*
2. Visual Studio 2017 with SDK 17134

### Compile and run

* For XAML based application with Unity rendering component open webrtc\windows\solutions\WebRtcUnityXaml.sln and build PeerConnectionClientUnity.WebRtc
* Unity 3D Peer Connection Client application - build PeerConnectionClientUnity project in webrtc\windows\solutions\WebRtcUnityD3D.sln

### Exporting Visual Studio solution from Unity Editor - PeerCC Unity standalone application only

1. Build soulution WebRtcUnityD3D.sln - this step adds WebRTC binaries to Unity project space
2. Open Unity project common\windows\samples\PeerCC\ClientUnity\Unity\PeerCCUnity in Unity Editor
3. Go to 'File' -> 'Build settings...' -> 'Build' and choose an export folder
4. Add the following XML block to PeerCCUnity\Package.appxmanifest:
```
  <Extensions>
    <Extension Category="windows.activatableClass.inProcessServer">
      <InProcessServer>
        <Path>WebRtcScheme.dll</Path>
        <ActivatableClass ActivatableClassId="WebRtcScheme.SchemeHandler" ThreadingModel="both" />
      </InProcessServer>
    </Extension>
  </Extensions>
```
5. Open PeerCCUnity.sln, build and run project PeerCCUnity

## Signalling server

The [signalling server](../Server) included in the project is a simple command line executable which accepts socket connections on port 8888.  Client activity is logged to the console window during execution.  A standard Azure Windows 10 VM is sufficient for hosting the server as long as port 8888 is opened.  Client code for interacting with the server is available [here](../ClientCore/Signalling/).
>**Note:** There are known issues with running the signalling server on one of the machines used as a peer for a call.  It is recommended that the server be run on a separate machine.  Note: Both peers must be able to access the signalling server directly.  Ensure that the IP is visible if unable to connect.

## Starting a call

1. Ensure the signalling server is running.
2. Launch the PeerConnection sample on 2 machines.
>**Note:** Different versions of the sample are able to communicate with each other.  For example, a HoloLens device running the Unity sample is able to establish a call with a Desktop device running the UWP code.

3. On each machine, click the gear icon and enter settings:
 1. **IP:** IP address of the signalling server
 2. **Port:** Port of the signalling server (8888 by default if using the provided exe)
 3. **Cameras:** Capture device for Video
 4. **Microphones/Speakers:** Due to WebRTC API limitations this is not currently selectable
 5. **Audio/Video Codecs:** Selects the default codecs, resolution & sampling rate.
 >**Note:** Codecs are determined by the initiator of the call, so only the sampling rate and resolution on the recipient are relevant.

4. Click the gear icon again to close the settings window.  Settings are persisted across application launches.
5. On both peers click "Connect" to connect to the signalling server.  Each peer should be visible in the list of available peers.
6. Click the microphone and camera icons below the list of available peers to disable audio or video.  It is recommended to disable audio when completing a call in the same room to avoid feedback.
7. On the initiator side, select the recipient peer and click the phone icon to start a call.
>**Note:** When running the HoloLens Unity sample it is recommended to start the call on the HoloLens device, which by default will choose the hardware accelerated H.264 codec for video.

8. The recipient will automatically accept the call.

## HoloLens Support
All of the above solutions run on HoloLens.  When running the UnitD3D or UnityXaml samples it is recommended to initiate the call on the HoloLens device so that optimal codecs and resolutions are chosen by default.  Some notes on the Unity solutions:

- There are known performance issues with Debug builds.  Recommended to build for Release/x86
- The capture camera does not run in the emulator, though the sample will launch.
- Keyboard input is not supported on the device, instead use virtual input on the [device portal](https://docs.microsoft.com/en-us/windows/mixed-reality/using-the-windows-device-portal) to enter text.
- The Unity application UI is located based on the position of the device when the app first launches.  You may need to look around a bit to see the UI if it's not immediately visible.
- The Unity applicaiton has minimal UI and does not allow setting the codecs/resolution.  To change the default, edit Assembly-CSharp.ControlScript.Initialize().
- Mixed-Reality Capture can be enabled or disabled at the application level by specifying CaptureCapability.MrcEnabled on Conductor.Instance.VideoCaptureProfile.  See Assembly-CSharp.ControlScript.Initialize() for an example.

### Access to MediaFoundation primitives for pose data
A way to extract CameraProjectionTransform and CameraViewTransform matrices in application layer has been added. For every video frame received from camera device a callback method which could be attached from application is called. The callback passes pointer value to IMFSample obtained from camera in synchronous way. Here is a sample code with dummy event handler:

    Media media = Media.CreateMedia();
    MFSampleVideoSource mfSampleSource = media.CreateMFSampleVideoSource();
    mfSampleSource.OnMFSampleVideoFrame += (s) => {
        Debug.WriteLine("MfSample received: " + s);
    };
Pointer to MFSample is passed to the application layer as uint64 WinRT type. Callback should be executed in C++/CX environment and pointer value should be cast to IMFSample*. A reference to Wold Coordinate System can be set from Unity environment using WorldManager.GetNativeISpatialCoordinateSystemPtr() method. There is a sample from [MixedRemoteViewCompositor](
https://github.com/Microsoft/MixedRealityCompanionKit/blob/master/MixedRemoteViewCompositor/Samples/LowLatencyMRC/Unity/Assets/AddOns/MixedRemoteViewCompositor/Scripts/MrvcCapture.cs#L92).

See [this issue](https://github.com/webrtc-uwp/webrtc-uwp-sdk/issues/30) on GitHub for further discussion.

### Syncing pose data with video frames (an alternative to MRC)
For complete discussion see [this issue](https://github.com/webrtc-uwp/webrtc-uwp-sdk/issues/10).

[WebRTC-UWP-CameraTransform-m62-VP8.zip](https://github.com/webrtc-uwp/webrtc-uwp-sdk/files/1861404/WebRTC-UWP-CameraTransform-m62-VP8.zip)

Please find attached the patches that we applied to m62 build to enable camera transform synchronization. We’re using UrhoSharp for both desktop/HoloLens but it should be similar for Unity. Those patches are only for VP8 but it’s straightforward to add H264 codec later. Below are some notes:

Our approach bases on this sample: https://github.com/Microsoft/MixedRealityCompanionKit/tree/master/MixedRemoteViewCompositor
There are 2 patches:
1.	Native: there are two extensions: CameraPosition(x, y, z) and CameraRotation(x, y, z, w). The reason why we need two is that the maximum bytes of a RTP header extension is 16. For the camera transform, we need 28 bytes in total.
2.	UWP: we add a new method called SetSpatialCoordinateSystem so that we can pass in the coordinate system of the reference frame from the app. This is required to calculate the correct worldToCamera matrix. Since we use VP8, we also change the signature of RawVideoFrame method to include the position and rotation parameters. The app can just use those data to render with synchronized transformation.
In the desktop app, we’re using these parameters for the projection matrix so that it matches HoloLens:
 - Fov: 27.15
 - NearClip: 0.1
 - FarClip: 1000
 - AspectRatio: width / height

(The better way to do is reading the projection matrix from IMFSample and send it to the desktop client using data channel)
Additionally, we need to do some math to convert the position/rotation from right-handed to left-handed coordinate system to use in UrhoSharp.

## WebRTC Tracing
Detailed tracing data is also available if the above options are insufficient to troubleshoot a problem.  To collect WebRTC Traces, follow these steps:

1. Run a TCP server on a remote machine and listen on port 55000.  Two examples of server software are Hercules TCP server for Windows or SocketTest TCP server for Mac.
2. Get the IP address of the machine running the TCP server.  This address should be entered into the ChatterBox application settings (Step 3).
3. In the ChatterBox settings page, enter the IP of the TCP server in the `RTC Trace server Ip` field.
4. Set the `RTC Trace server Port` to 55000.
5. Switch On `RTC Trace`. Use the toggle button on settings page to enable tracing.
