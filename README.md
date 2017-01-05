# Web Real-Time Communications (WebRTC) samples for the Universal Windows Platform

This repo contains several example which demonstrate the use of WebRTC in Universal Windows Platform apps.  The samples utilize the [Microsoft WebRTC for UWP Nuget package](http://www.nuget.org/packages/WebRtc/1.54.0-Alpha) to implement various WebRTC features, including a full featured Voice-Over-IP application for all Windows 10 platforms leveraging the [Windows.ApplicationModel.Calls](https://msdn.microsoft.com/library/windows/apps/windows.applicationmodel.calls.aspx) namespace.  The samples demonstrate the implementation of a UWP client which runs on Desktop, Mobile, Xbox, HoloLens and IoT devices.

## Samples

- <a href="Samples/ChatterBox-Sample">ChatterBox WebRTC VoIP Sample</a> This is the most compresive sample in the repository, demonstrating how to implement a full featured VoIP client for the Universal Windows Platform with WebRTC.  See the README.md file for a complete description of the technologies implemented.


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