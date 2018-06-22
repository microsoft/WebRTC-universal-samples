using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Capture;

namespace PeerConnectionClient.Ortc
{
    public class CaptureCapability
    {
        public uint Width { get; set; }
        public uint Height { get; set; }
        public uint FrameRate { get; set; }
        public String FullDescription { get; set; }
        public String ResolutionDescription { get; set; }
        public String FrameRateDescription { get; set; }
        public MediaRatio PixelAspectRatio { get; set; }
    }
    public class MediaDevice
    {
        public MediaDevice(string id, string name)
        {
            Id = id;
            Name = name;
        }

        public string Id { get; set; }
        public string Name { get; set; }

        public IAsyncOperation<List<CaptureCapability>> GetVideoCaptureCapabilities()
        {
            MediaDevice device = this;
            return Task.Run(async () =>
            {
                var settings = new MediaCaptureInitializationSettings()
                {
                    VideoDeviceId = device.Id,
                    MediaCategory = MediaCategory.Communications,
                };
                using (var capture = new MediaCapture())
                {
                    await capture.InitializeAsync(settings);
                    var caps =
                        capture.VideoDeviceController.GetAvailableMediaStreamProperties(
                            MediaStreamType.VideoRecord);

                    var arr = new List<CaptureCapability>();
                    foreach (var cap in caps)
                    {
                        if (cap.Type != "Video")
                        {
                            continue;
                        }

                        var videoCap = (VideoEncodingProperties) cap;

                        if (videoCap.FrameRate.Denominator == 0 ||
                            videoCap.FrameRate.Numerator == 0 ||
                            videoCap.Width == 0 ||
                            videoCap.Height == 0)
                        {
                            continue;
                        }
                        var captureCap = new CaptureCapability()
                        {
                            Width = videoCap.Width,
                            Height = videoCap.Height,
                            FrameRate = videoCap.FrameRate.Numerator/videoCap.FrameRate.Denominator,
                        };
                        captureCap.FrameRateDescription = $"{captureCap.FrameRate} fps";
                        captureCap.ResolutionDescription = $"{captureCap.Width} x {captureCap.Height}";
                        /*captureCap.PixelAspectRatio = new MediaRatio()
                {
                    Numerator = videoCap.PixelAspectRatio.Numerator,
                    Denominator = videoCap.PixelAspectRatio.Denominator,
                };*/
                        captureCap.FullDescription =
                            $"{captureCap.ResolutionDescription} {captureCap.FrameRateDescription}";
                        arr.Add(captureCap);
                    }
                    return arr.GroupBy(o => o.FullDescription).Select(o => o.First()).ToList();
                }
            }).AsAsyncOperation();
        }
    }
}