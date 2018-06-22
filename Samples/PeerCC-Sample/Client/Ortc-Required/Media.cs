using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media.Core;
using Org.Ortc;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using Org.Ortc.Adapter;
using PeerConnectionClient.Ortc.Utilities;

namespace PeerConnectionClient.Ortc
{
    public class RTCPeerConnectionHealthStats
    {
    }
    public class MediaStreamEvent
    {
        public MediaStream Stream;
    }

    public class RTCMediaStreamConstraints
    {
        public Boolean audioEnabled;
        public Boolean videoEnabled;
    }
    public class Media
    {
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        private MediaDevice _audioCaptureDevice;
        private MediaDevice _videoDevice;
        private int _preferredFrameWidth;
        private int _preferredFrameHeight;
        private int _preferredFPS;

        public delegate void OnMediaCaptureDeviceFoundDelegate(MediaDevice param0);

        public static Media CreateMedia()
        {
            var ret = new Media();

            return ret;
        }

        public static IAsyncOperation<Media> CreateMediaAsync()
        {
            return Task.Run(() => CreateMedia()).AsAsyncOperation();
        }


        public Task<IList<MediaStreamTrack>> GetUserMedia(RTCMediaStreamConstraints mediaStreamConstraints)
        {
            return Task.Run(() =>
            {
                var constraints = Helper.MakeConstraints(mediaStreamConstraints.audioEnabled, null,
                    MediaDeviceKind.AudioInput, _audioCaptureDevice);
                constraints = Helper.MakeConstraints(mediaStreamConstraints.videoEnabled, constraints,
                    MediaDeviceKind.VideoInput, _videoDevice);
                if (constraints.Video != null && constraints.Video.Advanced.Count > 0)
                {
                    MediaTrackConstraintSet constraintSet = constraints.Video.Advanced.First();
                    constraintSet.Width = new ConstrainLong { Value = _preferredFrameWidth };
                    constraintSet.Height = new ConstrainLong { Value = _preferredFrameHeight };
                    constraintSet.FrameRate = new ConstrainDouble { Value = _preferredFPS };
                }

                Task<IList<MediaStreamTrack>> task = MediaDevices.GetUserMedia(constraints).AsTask();
                return task.Result;
            });
        }
        public IMediaSource CreateMediaSource(MediaStreamTrack track, string id)
        {
            return track?.CreateMediaSource();
        }

        public static void OnAppSuspending()
        {
            MediaDevices.OnAppSuspending();
        }

        public void SelectAudioCaptureDevice(MediaDevice device)
        {
            using (var @lock = new AutoLock(_lock))
            {
                @lock.WaitAsync().Wait();
                _audioCaptureDevice = device;
            }
        }

        public void SelectVideoDevice(MediaDevice device)
        {
            using (var @lock = new AutoLock(_lock))
            {
                @lock.WaitAsync().Wait();
                _videoDevice = device;
            }
        }

        public void SetPreferredVideoCaptureFormat(int frameWidth, int frameHeight, int fps)
        {
            using (var @lock = new AutoLock(_lock))
            {
                @lock.WaitAsync().Wait();
                _preferredFrameWidth = frameWidth;
                _preferredFrameHeight = frameHeight;
                _preferredFPS = fps;
            }
        }
    }
}