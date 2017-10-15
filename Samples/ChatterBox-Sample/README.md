<!--
  category: Communications
  samplefwlink: http://go.microsoft.com/fwlink/p/?LinkId=??????
-->

# Web Real-Time Communications (WebRTC) Voice over IP (VoIP) sample (aka ChatterBox)

> **Note: This sample is no longer maintained.**  While the sample shows proper use of the VoIP APIs in Windows, we recommend the PeerConnection sample as the best implementation of a WebRTC client on Windows:  https://github.com/Microsoft/WebRTC-universal-samples/tree/master/Samples/PeerCC-Sample This sample remains as a demonstration of Windows VoIP APIs.  For further information on the VoIP APIs please visit: https://github.com/Microsoft/Windows-universal-samples/tree/master/Samples/VoIP


> **Full WebRTC for UWP Source & Samples**
The samples in this repository are a mirror of the full WebRTC UWP source which can be found below.  For the most up to date samples, and the complete source for the WebRTC UWP port, please visit: https://github.com/webrtc-uwp

Utilizes the [Microsoft WebRTC for UWP Nuget package](http://www.nuget.org/packages/WebRtc/) to implement a full featured Voice-Over-IP application for all Windows 10 platforms leveraging the [Windows.ApplicationModel.Calls](https://msdn.microsoft.com/library/windows/apps/windows.applicationmodel.calls.aspx) namespace.  This sample demonstrates the implementation of a UWP client which runs on Desktop, Mobile, Xbox and HoloLens and a signalling server component.  The resulting solution features VoIP audio and video calling, text messaging, user presence (here, away, offline), receiving of calls when the application is suspended, closed or the device is rebooted, and more.  Out of the box, the application allows for two way calls between remote parties over various network topologies and NAT conditions and has been tested on all Windows 10 platforms.  The sample also demonstrates key technologies required to implement a full VoIP solution on Windows 10 such as maintaining a call when the app is backgrounded, efficient rendering from a background process to a foreground application via SwapChainPanel, WASAPI audio rendering, and Windows 10 dialer integration.

> **Note:** This sample is one of many WebRTC for UWP samples and hands on labs.  The source code for the underlying WebRTC for UWP port is available as well and will be made public on GitHub shortly.  In the interim, please contact [jacadd@microsoft.com](mailto:jacadd@microsoft.com) for access.

> **Note:** While this sample is not appropriate for IoT platforms, the underlying WebRTC for UWP Library does work on Windows 10 IoT.  Please contact [jacadd@microsoft.com](mailto:jacadd@microsoft.com) for additional support if you plan to use this library on IoT devices.


> **Note:** This sample is part of a large collection of UWP feature samples.
> If you are unfamiliar with Git and GitHub, you can download the entire collection as a
> [ZIP file](https://github.com/Microsoft/Windows-universal-samples/archive/master.zip), but be
> sure to unzip everything to access shared dependencies. For more info on working with the ZIP file,
> the samples collection, and GitHub, see [Get the UWP samples from GitHub](https://aka.ms/ovu2uq).
> For more samples, see the [Samples portal](https://aka.ms/winsamples) on the Windows Dev Center.

In this Sample, the user can initiate an incoming or outgoing call.
WASAPI has been implemented into the sample to provide audio loopback. It simply connects to localhost.

This Sample utilizes Windows Mobile Extensions for UWP and will only work on mobile devices with right capabilities.

## System requirements

**Client:** Windows 10

**Server:** Windows Server 2016 Technical Preview

**Phone:**  Windows 10

**Xbox:** Windows 10

**HoloLens:** Windows 10


## Build the sample

1. If you download the samples ZIP, be sure to unzip the entire archive, not just the folder with the sample you want to build.
2. Start Microsoft Visual Studio 2015 and select **File** \> **Open** \> **Project/Solution**.
3. Starting in the folder where you unzipped the samples, go to the Samples subfolder, then the subfolder for this specific sample, then the subfolder for C#. Double-click the Visual Studio 2015 Solution (.sln) file.
4. Press Ctrl+Shift+B, or select **Build** \> **Build Solution**.

> **Note:** There are 2 projects that are valid startup projects.  **ChatterBox (Universal Windows)** is a client application that can be run on any Windows 10 platform and **ChatterBox.Server** which is intended to be run on a Desktop machine or Azure VM and provides signalling to the client.

### Deploying the sample

- Select Build > Deploy Solution.

### Deploying and running the sample

- To debug the sample and then run it, press F5 or select Debug >  Start Debugging. To run the sample without debugging, press Ctrl+F5 or selectDebug > Start Without Debugging.

## Making a call between two peers

This sample demonstrates completion of a VoIP audio/video call between two peers.  In order to make a call, three components are required:

- Two clients, each running the ChatterBox (Universal Windows) app.
- One server, running the Chatterbox.Server app.  Each client must be able to access the server IP - Azure VMs (preferably with a static IP) work well for the signalling server.  One of the client devices may be used as a server as well to reduce the number of devices required.

> **Note:** Call data is **not** routed through the above server.  The purpose of the server is to provide signalling between the two clients; it does not participate in the network pipeline during a call.


### Prepare to make a call

1. Start ChatterBox.Server on a Desktop machine or Azure VM.  Note the machine's IP Address - the clients must be able to reach the server IP in order to establish a connection (though call data is not routed through the server).
2. Start ChatterBox (Universal Windows) on two client devices - this can be any of the supported platforms listed above.  This will also launch the BackgroundHost process which contains triggers to resume the background process when the client app is suspended or Windows is rebooted.
3. In the ChatterBox app, access the settings page and input the server IP address in the "Server Host" field.
4. Save the settings, then press the connect button.

### Making a call
1. Select a user to call from the user list in the ChatterBox client application.
2. Press the audio or video button to initiate an audio or video call.
3. The remote peer will receive notification of an incoming call (even if the client app is not in the foreground or the machine has been rebooted since launching the ChatterBox client).
4. Answer the call by pressing the accept button.

## Visual Studio plugin, Application Insights and tracing

The WebRTC for UWP Library contains deep instrumentation in several forms, providing the developer with extensive options to diagnose call and connectivity issues.

### Visual Studio plugin & ETW events
A Visual Studio 2015 extension has been developed to visualize WebRTC statistics and events in Visual Studio diagnostic tools in real-time, during a calling session.  Utilizing this tool it is possible to view markers for key events such as call establishment and ICE negotiation, visualize network bandwidth and framerate changes, and more.  The ChatterBox application exposes this data as Event Tracing for Windows (ETW) events which are visualized by the Visual Studio plugin.  Additionally, any ETW compatible tool such as the [Media eXperience Analyzer](https://www.microsoft.com/en-us/download/details.aspx?id=43105) can be used to analyze the events emitted by the ChatterBox app.

To be able to see the statistics and events in the diagnostic tool for the ChatterBox application, you will need to turn on ETW Statistics before establishing a call. You can toggle this option on in the settings page of the ChatterBox application.  This option is often used in conjunction with the Windows Device Portal to create a log for later analysis.  To create an ETW log:

1. Navigate to the device portal (enable this in Settings\Update & Security\For Developers).
2. Choose "Performance tracing".
3. (Optional) Click "Browse"  under "Custom profiles" and open a tracing profile (.wprp).  Alternatively, select the desired providers and events to record.
4. Click "Start Trace".
5. Use the application to complete a call.
6. Click "Stop Trace".
7. The trace will appear under the Traces section at the bottom of the page
8. Click the "Save" button next to the trace to save it locally.

> **Note:** Traces can be very large.  Ideally record about 60 seconds or less of an issue, no more than 2 minutes.

### Application Insights logging
Users can switch on the "AppInsight Logging" setting to send metrics & logs to an Application Insights resource in Azure.  To enable this functionality, update `<InstrumentationKey>` in ChatterBox/ApplicationInsights.config file to contain the GUID for your Azure Application Insights instance.

#### The following custom events are logged in Application Insights:

 - **CallStarted:** This event is logged when incoming/outgoing calls are started. It contains `Timestamp` and `Connection Type` custom data fields.
 - **CallEnded:** This event is logged when calls are ended. It contain the custom fields `Timestamp` and `Call Duration`.
 - **Network Average Quality:** This event is logged at the end of each call. It captures `Timestamp`, `Minimum Inbound Speed` and `Maximum Outbound Speed` for each call. These are average speeds read from the network adapter.
 - **Audio Codec:** This event is logged at the beginning of the call.
 - **Video Codec:** This event is logged at the beginning of the call.
 - **Video Height/Width Downgrade:** This event is logged during the call if the current video resolution (height/width) is changed during the call.
 - **Application Suspending/Resuming:** This event is logged when the application is suspended or resumed.

#### The following metrics can be found in the "Metrics Explorer" of the Application Insights resource:

 - **Old/New Height/Width:** These metrics are from `Video Height/Width Downgrade` event.
 - **Minimum Inbound/Maximum Outbound Speed:** These metrics are taken from the `Network Average Quality` event.
 - **Audio/Video Packet Lost Rate:** The metric is logged once per minute during a call. It is equal to <sum of sent audio/video packets>/<sum of lost audio/video packets>.
 - **Audio/Video Current Delay Rate:** The metric is logged after call end. It is equal to <average of audio/video current delays (received from WebRTC)>.

### WebRTC Tracing
Detailed tracing data is also available if the above options are insufficient to troubleshoot a problem.  To collect WebRTC Traces, follow these steps:

1. Run a TCP server on a remote machine and listen on port 55000.  Two examples of server software are Hercules TCP server for Windows or SocketTest TCP server for Mac.
2. Get the IP address of the machine running the TCP server.  This address should be entered into the ChatterBox application settings (Step 3).
3. In the ChatterBox settings page, enter the IP of the TCP server in the `RTC Trace server Ip` field.
4. Set the `RTC Trace server Port` to 55000.
5. Switch On `RTC Trace`. Use the toggle button on settings page to enable tracing.
