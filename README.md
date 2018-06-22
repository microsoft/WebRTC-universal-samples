# Web Real-Time Communications (WebRTC) samples for the Universal Windows Platform

This repo contains several example which demonstrate the use of WebRTC in Universal Windows Platform apps.  The samples utilize the [Microsoft WebRTC for UWP Nuget package](http://www.nuget.org/packages/WebRtc) and run on Desktop, Xbox & HoloLens devices.

## Full WebRTC for UWP Source & Samples
The samples in this repository are a mirror of the full WebRTC UWP source which can be found below.  For the most up to date samples, and the complete source for the WebRTC UWP port, please visit: https://github.com/webrtc-uwp

## Samples

- [PeerConnection Client](Samples/PeerCC-Sample) This is the recommended sample for getting started with WebRTC on UWP.  It's a port of the WebRTC.org PeerConnection sample and is compatible with the same sample on iOS, Android, running in Chrome, etc.  It also now includes **Unity** and [Mixed-Reality Capture](https://docs.microsoft.com/en-us/windows/mixed-reality/mixed-reality-capture-for-developers) support for HoloLens.
- [ChatterBox](Samples/ChatterBox-Sample) **Deprecated:** This sample remains as a reference for a full VoIP implementation on UWP but is not actively maintained.


## Universal Windows Platform development

These samples require Visual Studio 2015 and the Windows Software Development Kit (SDK) for Windows 10 to build, test, and deploy your Universal Windows Platform apps.

   [Get a free copy of Visual Studio 2015 Community Edition with support for building Universal Windows Platform apps](http://go.microsoft.com/fwlink/p/?LinkID=280676)

Additionally, to stay on top of the latest updates to Windows and the development tools, become a Windows Insider by joining the Windows Insider Program.

   [Become a Windows Insider](https://insider.windows.com/)

## Using the samples

The easiest way to use these samples without using Git is to download the zip file containing the current version (using the following link or by clicking the "Download ZIP" button on the repo page). You can then unzip the entire archive and use the samples in Visual Studio 2015.

   [Download the samples ZIP](../../archive/master.zip)

   **Notes:**
   * Before you unzip the archive, right-click it, select **Properties**, and then select **Unblock**.
   * Be sure to unzip the entire archive, and not just individual samples. The samples all depend on the SharedContent folder in the archive.   
   * In Visual Studio 2015, the platform target defaults to ARM, so be sure to change that to x64 or x86 if you want to test on a non-ARM device.

The samples use Linked files in Visual Studio to reduce duplication of common files, including sample template files and image assets. These common files are stored in the SharedContent folder at the root of the repository, and are referred to in the project files using links.

**Reminder:** If you unzip individual samples, they will not build due to references to other portions of the ZIP file that were not unzipped. You must unzip the entire archive if you intend to build the samples.

## Contributions

We're interested to work with the community to make this repository as useful as possible.  Please contact us about any contributions and we'll work with you to make them available here.
