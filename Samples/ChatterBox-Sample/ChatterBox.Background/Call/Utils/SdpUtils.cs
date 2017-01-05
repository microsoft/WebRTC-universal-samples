//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using System.Collections.Generic;
using System.Text.RegularExpressions;
using Org.WebRtc;

namespace ChatterBox.Background.Call.Utils
{
    internal class SdpUtils
    {
        /// <summary>
        ///     Forces the SDP to use the selected audio and video codecs.
        /// </summary>
        /// <param name="sdp">Session description.</param>
        /// <param name="audioCodec">Audio codec.</param>
        /// <param name="videoCodec">Video codec.</param>
        /// <returns>True if succeeds to force to use the selected audio/video codecs.</returns>
        public static bool SelectCodecs(ref string sdp, CodecInfo audioCodec, CodecInfo videoCodec)
        {
            var mfdRegex = new Regex("\r\nm=audio.*RTP.*?( .\\d*)+\r\n");
            var mfdMatch = mfdRegex.Match(sdp);
            var mfdListToErase = new List<string>(); //mdf = media format descriptor
            var audioMediaDescFound = mfdMatch.Groups.Count > 1; //Group 0 is whole match
            if (audioMediaDescFound)
            {
                if (audioCodec == null)
                {
                    return false;
                }
                for (var groupCtr = 1 /*Group 0 is whole match*/; groupCtr < mfdMatch.Groups.Count; groupCtr++)
                {
                    for (var captureCtr = 0; captureCtr < mfdMatch.Groups[groupCtr].Captures.Count; captureCtr++)
                    {
                        mfdListToErase.Add(mfdMatch.Groups[groupCtr].Captures[captureCtr].Value.TrimStart());
                    }
                }
                if (!mfdListToErase.Remove(audioCodec.Id.ToString()))
                {
                    return false;
                }
            }

            mfdRegex = new Regex("\r\nm=video.*RTP.*?( .\\d*)+\r\n");
            mfdMatch = mfdRegex.Match(sdp);
            var videoMediaDescFound = mfdMatch.Groups.Count > 1; //Group 0 is whole match
            if (videoMediaDescFound)
            {
                if (videoCodec == null)
                {
                    return false;
                }
                for (var groupCtr = 1 /*Group 0 is whole match*/; groupCtr < mfdMatch.Groups.Count; groupCtr++)
                {
                    for (var captureCtr = 0; captureCtr < mfdMatch.Groups[groupCtr].Captures.Count; captureCtr++)
                    {
                        mfdListToErase.Add(mfdMatch.Groups[groupCtr].Captures[captureCtr].Value.TrimStart());
                    }
                }
                if (!mfdListToErase.Remove(videoCodec.Id.ToString()))
                {
                    return false;
                }
            }

            if (audioMediaDescFound)
            {
                // Alter audio entry
                var audioRegex = new Regex("\r\n(m=audio.*RTP.*?)( .\\d*)+");
                sdp = audioRegex.Replace(sdp, "\r\n$1 " + audioCodec.Id);
            }

            if (videoMediaDescFound)
            {
                // Alter video entry
                var videoRegex = new Regex("\r\n(m=video.*RTP.*?)( .\\d*)+");
                sdp = videoRegex.Replace(sdp, "\r\n$1 " + videoCodec.Id);
            }

            // Remove associated rtp mapping, format parameters, feedback parameters
            var removeOtherMdfs = new Regex("a=(rtpmap|fmtp|rtcp-fb):(" + string.Join("|", mfdListToErase) + ") .*\r\n");
            sdp = removeOtherMdfs.Replace(sdp, "");

            return true;
        }

        internal static bool IsHold(string sdp)
        {
            // If the payload doesn't send any media, we consider it a call on hold.
            return !sdp.Contains("a=send");
        }

        internal static List<int> GetVideoCodecIds(string sdp)
        {
            var mfdRegex = new Regex("\r\nm=video.*RTP.*?( .\\d*)+\r\n");
            var mfdMatch = mfdRegex.Match(sdp);
            var mfdList = new List<int>(); //mdf = media format descriptor
            var videoMediaDescFound = mfdMatch.Groups.Count > 1; //Group 0 is whole match
            if (videoMediaDescFound)
            {
                for (var groupCtr = 1 /*Group 0 is whole match*/; groupCtr < mfdMatch.Groups.Count; groupCtr++)
                {
                    for (var captureCtr = 0; captureCtr < mfdMatch.Groups[groupCtr].Captures.Count; captureCtr++)
                    {
                        string codecId = mfdMatch.Groups[groupCtr].Captures[captureCtr].Value.TrimStart();
                        mfdList.Add(int.Parse(codecId));
                    }
                }
            }
            return mfdList;
        }
    }
}