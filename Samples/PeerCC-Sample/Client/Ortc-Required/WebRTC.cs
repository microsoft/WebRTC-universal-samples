using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime;
using Windows.UI.Core;
using Windows.Storage;
using Windows.Foundation;
using Windows.Media.Capture;
using Org.Ortc;
using PeerConnectionClient.Ortc.Utilities;

namespace PeerConnectionClient.Utilities
{
    public enum LogLevel
    {
        LOGLVL_SENSITIVE = 0,
        LOGLVL_VERBOSE = 1,
        LOGLVL_INFO = 2,
        LOGLVL_WARNING = 3,
        LOGLVL_ERROR = 4
    }

    public class WebRTC
    {
        public static void DisableLogging()
        {

        }

        public static void EnableLogging(LogLevel level)
        {

        }

        public static IList<RTCRtpCodecCapability> GetAudioCodecs()
        {
            return Helper.GetCodecs("audio");
        }

        //public static double GetCPUUsage();
        //public static long GetMemUsage();
        public static IList<RTCRtpCodecCapability> GetVideoCodecs()
        {
            return Helper.GetCodecs("video");
        }

        public static void Initialize(CoreDispatcher dispatcher)
        {
            Settings.ApplyDefaults();
            OrtcWithDispatcher.Setup(dispatcher);
        }

        //public static bool IsTracing();
        public static string LogFileName()
        {
            return "";
        }

        public static StorageFolder LogFolder()
        {
            return null;
        }

        private static async Task<bool> RequestAccessForMediaCapturePrivate() //async
        {
            MediaCapture mediaAccessRequester = new MediaCapture();
            MediaCaptureInitializationSettings mediaSettings = new MediaCaptureInitializationSettings
            {
                AudioDeviceId = "",
                VideoDeviceId = "",
                StreamingCaptureMode = StreamingCaptureMode.AudioAndVideo,
                PhotoCaptureSource = PhotoCaptureSource.VideoPreview
            };

            await mediaAccessRequester.InitializeAsync(mediaSettings);

            if (mediaAccessRequester.MediaCaptureSettings.VideoDeviceId != "" &&
                mediaAccessRequester.MediaCaptureSettings.AudioDeviceId != "")
            {
                return true;
            }

            return false;
        }

        public static IAsyncOperation<bool> RequestAccessForMediaCapture()
        {
            return RequestAccessForMediaCapturePrivate().AsAsyncOperation();
        }

        //[Overload("SaveTrace2")]
        //public static bool SaveTrace(string filename);
        //[Overload("SaveTrace1")]
        public static bool SaveTrace(string host, int port)
        {
            return false;
        }

        public static void SetPreferredVideoCaptureFormat(int frameWidth, int frameHeight, int fps)
        {

        }

        public static void StartTracing()
        {

        }

        public static void StopTracing()
        {

        }

        public static void UpdateCPUUsage(double cpuUsage)
        {

        }

        public static void UpdateMemUsage(long memUsage)
        {

        }

        /// <summary>
        /// CPU usage statistics data (in percents). Should be set by application.
        /// </summary>
        public static double CpuUsage { get; set; }

        /// <summary>
        /// Memory usage statistics data (in bytes). Should be set by application.
        /// </summary>
        public static Int64 MemoryUsage { get; set; }
    }

}