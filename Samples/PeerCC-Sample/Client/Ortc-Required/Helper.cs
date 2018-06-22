using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.System.Profile;
using Org.Ortc;
using Org.Ortc.Adapter;
using PeerConnectionClient.Ortc;

namespace PeerConnectionClient.Ortc.Utilities
{ 
    class Helper
    {
        public static MediaDevice ToMediaDevice(MediaDeviceInfo device)
        {
            return new MediaDevice(device.DeviceId, device.Label);
        }

        public static IList<RTCRtpCodecCapability> GetCodecs(string kind)
        {
            var caps = RTCRtpSender.GetCapabilities(kind);
            var results = new List<RTCRtpCodecCapability>(caps.Codecs);
            return results;
        }
                
        public static MediaStreamConstraints MakeConstraints(
            bool shouldDoThis,
            MediaStreamConstraints existingConstraints,
            MediaDeviceKind kind,
            MediaDevice device
            )
        {
            if (!shouldDoThis) return existingConstraints;
            if (null == device) return existingConstraints;

            if (null == existingConstraints) existingConstraints = new MediaStreamConstraints();
            MediaTrackConstraints trackConstraints = null;

            switch (kind)
            {
                case MediaDeviceKind.AudioInput:
                    trackConstraints = existingConstraints.Audio;
                    break;
                case MediaDeviceKind.AudioOutput:
                    trackConstraints = existingConstraints.Audio;
                    break;
                case MediaDeviceKind.VideoInput:
                    trackConstraints = existingConstraints.Video;
                    break;
            }
            if (null == trackConstraints) trackConstraints = new MediaTrackConstraints();

            if (null == trackConstraints.Advanced)
                trackConstraints.Advanced = new List<MediaTrackConstraintSet>();

            var constraintSet = new MediaTrackConstraintSet
            {
                DeviceId = new ConstrainString
                {
                    Parameters = new ConstrainStringParameters
                    {
                        Exact = new StringOrStringList {Value = device.Id}
                    },
                    Value = new StringOrStringList {Value = device.Id}
                }
            };

            trackConstraints.Advanced.Add(constraintSet);

            switch (kind)
            {
                case MediaDeviceKind.AudioInput:
                    existingConstraints.Audio = trackConstraints;
                    break;
                case MediaDeviceKind.AudioOutput:
                    existingConstraints.Audio = trackConstraints;
                    break;
                case MediaDeviceKind.VideoInput:
                    existingConstraints.Video = trackConstraints;
                    break;
            }
            return existingConstraints;
        }
                
        public static RTCPeerConnectionSignalingMode SignalingModeForClientName(string clientName)
        {
            RTCPeerConnectionSignalingMode ret = RTCPeerConnectionSignalingMode.Json;

            string[] substring = clientName.Split('-');
            switch (substring.Last())
            {
                case "dual":
                    ret = RTCPeerConnectionSignalingMode.Json;
                    break;

                case "json":
                    ret = RTCPeerConnectionSignalingMode.Json; 
                    break;

                default:
                    ret = RTCPeerConnectionSignalingMode.Sdp;
                    break;
            }
            return ret;
        }

        public static Task<RTCMediaStreamTrackConfiguration> GetTrackConfigurationForCapabilities(RTCRtpCapabilities sourceCapabilities, RTCRtpCodecCapability preferredCodec)
        {
            if (preferredCodec == null)
                throw new ArgumentNullException(nameof(preferredCodec));

            return Task.Run(() =>
            {
                RTCRtpCapabilities capabilities = sourceCapabilities.Clone();
              
                // scoope: move prefered codec to be first in the list
                {
                    var itemsToRemove = capabilities.Codecs.Where(x => x.PreferredPayloadType == preferredCodec.PreferredPayloadType).ToList();
                    if (itemsToRemove.Count > 0)
                    {
                        RTCRtpCodecCapability codecCapability = itemsToRemove.First();
                        if (codecCapability != null && capabilities.Codecs.IndexOf(codecCapability) > 0)
                        {
                            capabilities.Codecs.Remove(codecCapability);
                            capabilities.Codecs.Insert(0, codecCapability);
                        }
                    }
                }

                RTCMediaStreamTrackConfiguration configuration = new RTCMediaStreamTrackConfiguration()
                {
                    Capabilities = capabilities
                };
                return configuration;
            });
        }

        public static string DeviceName()
        {
            EasClientDeviceInformation eas = new EasClientDeviceInformation();
            //DeviceManufacturer = eas.SystemManufacturer;//device manufacturer
            var ret = eas.SystemManufacturer + "-" + eas.SystemProductName;
            return ret;
        }

        public static string ProductName()
        {
            string ret;
            PackageVersion pv = Package.Current.Id.Version;
            var applicationVersion = $"{pv.Major}.{pv.Minor}.{pv.Build}.{pv.Revision}";
            ret=Package.Current.DisplayName + applicationVersion;
            return ret;
        }

        public static string OsVersion()
        {
            AnalyticsVersionInfo ai = AnalyticsInfo.VersionInfo;
            string systemFamily = ai.DeviceFamily;

            // get the system version number
            string sv = AnalyticsInfo.VersionInfo.DeviceFamilyVersion;
            ulong v = ulong.Parse(sv);
            ulong v1 = (v & 0xFFFF000000000000L) >> 48;
            ulong v2 = (v & 0x0000FFFF00000000L) >> 32;
            ulong v3 = (v & 0x00000000FFFF0000L) >> 16;
            ulong v4 = (v & 0x000000000000FFFFL);
            string systemVersion = $"{v1}.{v2}.{v3}.{v4}";
            var ret = systemFamily + "_" + systemVersion;
            return ret;
        }
    }
}
