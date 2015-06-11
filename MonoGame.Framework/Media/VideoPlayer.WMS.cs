﻿using System.Diagnostics;
using System.Threading;
using Microsoft.Xna.Framework.Graphics;
using SharpDX;
using SharpDX.MediaFoundation;
using SharpDX.Win32;
using System;
using System.Runtime.InteropServices;

namespace Microsoft.Xna.Framework.Media
{
    public sealed partial class VideoPlayer : IDisposable
    {
        private static MediaSession _session;
        private static AudioStreamVolume _volumeController;
        private static PresentationClock _clock;

        // HACK: Need SharpDX to fix this.
        private static Guid AudioStreamVolumeGuid;

        private static Callback _callback;

        private static Texture2D retTexture;

        private class Callback : IAsyncCallback
        {
            public void Dispose()
            {
            }

            public IDisposable Shadow { get; set; }
            public void Invoke(AsyncResult asyncResultRef)
            {
                var ev = _session.EndGetEvent(asyncResultRef);

                // Trigger an "on Video Ended" event here if needed

                _session.BeginGetEvent(this, null);
            }

            public AsyncCallbackFlags Flags { get; private set; }
            public WorkQueueId WorkQueueId { get; private set; }
        }

        private void PlatformInitialize()
        {
            // The GUID is specified in a GuidAttribute attached to the class
            AudioStreamVolumeGuid = Guid.Parse(((GuidAttribute)typeof(AudioStreamVolume).GetCustomAttributes(typeof(GuidAttribute), false)[0]).Value);

            MediaManagerState.CheckStartup();
            MediaFactory.CreateMediaSession(null, out _session);
        }

        private Texture2D PlatformGetTexture()
        {
            var sampleGrabber = _currentVideo.SampleGrabber;

            var texData = sampleGrabber.TextureData;

            if (texData == null)
                return null;

            // TODO: This could likely be optimized if we held on to the SharpDX Surface/Texture data,
            // and set it on an XNA one rather than constructing a new one every time this is called.
            if (retTexture == null || retTexture.IsDisposed || retTexture.Width != _currentVideo.Width || retTexture.Height != _currentVideo.Height)
            {
                if (retTexture != null && !retTexture.IsDisposed)
                {
                    retTexture.Dispose();
                }

                retTexture = new Texture2D(Game.Instance.GraphicsDevice, _currentVideo.Width, _currentVideo.Height, false, SurfaceFormat.Bgr32);
            }

            retTexture.SetData(texData);
            
            return retTexture;
        }

        private void PlatformGetState(ref MediaState result)
        {
            if (_clock != null)
            {
                ClockState state;
                _clock.GetState(0, out state);

                switch (state)
                {
                    case ClockState.Running:
                        result = MediaState.Playing;
                        return;

                    case ClockState.Paused:
                        result = MediaState.Paused;
                        return;
                }
            }

            result = MediaState.Stopped;
        }

        private void PlatformPause()
        {
            _session.Pause();
        }

        private void PlatformPlay()
        {
            // Cleanup the last song first.
            if (State != MediaState.Stopped)
            {
                _session.Stop();
                _volumeController.Dispose();
                _clock.Dispose();
            }

            // Set the new song.
            _session.SetTopology(0, _currentVideo.Topology);

            _volumeController = CppObject.FromPointer<AudioStreamVolume>(MediaPlayer.GetVolumeObj(_session));
            SetChannelVolumes();

            // Get the clock.
            _clock = _session.Clock.QueryInterface<PresentationClock>();

            //create the callback if it hasn't been created yet
            if (_callback == null)
            {
                _callback = new Callback();
                _session.BeginGetEvent(_callback, null);
            }

            // Start playing.
            var varStart = new Variant();
            _session.Start(null, varStart);
        }

        private void PlatformResume()
        {
            var varStart = new Variant();
            _session.Start(null, varStart);
        }

        private void PlatformStop()
        {
            _session.ClearTopologies();
            _session.Stop();
            _session.Close();
            _volumeController.Dispose();
            _volumeController = null;
            _clock.Dispose();
            _clock = null;
        }

        private void SetChannelVolumes()
        {
            if (_volumeController != null)
            {
                float volume = !_isMuted ? _volume : 0.0f;
                for (int i = 0; i < _volumeController.ChannelCount; i++)
                    _volumeController.SetChannelVolume(i, volume);
            }
        }

        private void PlatformSetVolume()
        {
            if (_volumeController == null)
                return;

            SetChannelVolumes();
        }

        private void PlatformSetIsLooped()
        {
            throw new NotImplementedException();
        }

        private void PlatformSetIsMuted()
        {
            if (_volumeController == null)
                return;

            SetChannelVolumes();
        }

        private TimeSpan PlatformGetPlayPosition()
        {
            return TimeSpan.FromTicks(_clock.Time);
        }

        private void PlatformDispose(bool disposing)
        {
            if (retTexture != null && !retTexture.IsDisposed)
            {
                retTexture.Dispose();
                retTexture = null;
            }
        }
    }
}
