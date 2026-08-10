using CapFrameX.Contracts.Configuration;
using CapFrameX.Extensions.NetStandard;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;

namespace CapFrameX.Data
{
    public class SoundManager
    {
        private readonly Dictionary<string, AudioFileReader> _audioFileDictionary
            = new Dictionary<string, AudioFileReader>(6);
        private readonly object _playbackLock = new object();
        private readonly IAppConfiguration _configuration;
        private readonly ILogger<SoundManager> _logger;

        /// <summary>Guarded by <see cref="_playbackLock"/>.</summary>
        private WaveOutEvent _activeDevice;

        public SoundMode SoundMode
        {
            get => Enum.TryParse(_configuration.HotkeySoundMode, out SoundMode soundMode) ? soundMode : SoundMode.Voice;
            set
            {
                _configuration.HotkeySoundMode = value.ConvertToString();
            }
        }

        public double Volume
        {
            get
            {
                switch (SoundMode)
                {
                    case SoundMode.Voice:
                        return _configuration.VoiceSoundLevel;
                    case SoundMode.Simple:
                        return _configuration.SimpleSoundLevel;
                    default:
                        return 0;
                }
            }
            set
            {
                switch (SoundMode)
                {
                    case SoundMode.Voice:
                        _configuration.VoiceSoundLevel = value;
                        break;
                    case SoundMode.Simple:
                        _configuration.SimpleSoundLevel = value;
                        break;
                    default:
                        break;
                }
            }
        }

        public string[] AvailableSoundModes => Enum.GetNames(typeof(SoundMode));

        public SoundManager(IAppConfiguration configuration, ILogger<SoundManager> logger)
        {
            _configuration = configuration;
            _logger = logger;
            string soundPath;

            void addPlayer(string path)
            {
                try
                {
                    var audioFile = new AudioFileReader(path);
                    _audioFileDictionary.Add(path, audioFile);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error while add player {path}.");
                }
            }

            // capture started (voice)
            soundPath = Path.Combine("Sounds", SoundMode.Voice.ConvertToString(), $"{Sound.CaptureStarted.ConvertToString()}.mp3");
            addPlayer(soundPath);

            // capture started (simple)
            soundPath = Path.Combine("Sounds", SoundMode.Simple.ConvertToString(), $"{Sound.CaptureStarted.ConvertToString()}.mp3");
            addPlayer(soundPath);

            // capture stopped (voice)
            soundPath = Path.Combine("Sounds", SoundMode.Voice.ConvertToString(), $"{Sound.CaptureStopped.ConvertToString()}.mp3");
            addPlayer(soundPath);

            // capture stopped (simple)
            soundPath = Path.Combine("Sounds", SoundMode.Simple.ConvertToString(), $"{Sound.CaptureStopped.ConvertToString()}.mp3");
            addPlayer(soundPath);

            // more than one process (voice)
            soundPath = Path.Combine("Sounds", SoundMode.Voice.ConvertToString(), $"{Sound.MoreThanOneProcess.ConvertToString()}.mp3");
            addPlayer(soundPath);

            // no process detected (voice)
            soundPath = Path.Combine("Sounds", SoundMode.Voice.ConvertToString(), $"{Sound.NoProcess.ConvertToString()}.mp3");
            addPlayer(soundPath);
        }

        public void PlaySound(Sound sound)
        {
            if ((SoundMode is SoundMode.Simple && (sound == Sound.MoreThanOneProcess || sound == Sound.NoProcess)) || SoundMode is SoundMode.None)
                return;

            var currentSoundMode = SoundMode;
            double currentVolume = Volume;
            var path = Path.Combine("Sounds", currentSoundMode.ConvertToString(), $"{sound.ConvertToString()}.mp3");

            // Missing when the file could not be loaded at construction time. Reported instead of
            // thrown, so a missing sound file cannot take an ongoing capture with it.
            if (!_audioFileDictionary.TryGetValue(path, out var audioFile))
            {
                _logger.LogError("No audio file loaded for {path}.", path);
                return;
            }

            // WaveOutEvent signals playback through its own thread and needs no message pump, so
            // this no longer runs through the dispatcher: playing a sound is not the UI thread's
            // business, and it used to block whoever asked for it — including the hotkey path.
            try
            {
                lock (_playbackLock)
                {
                    // One announcement at a time. The readers are shared instances, so a second
                    // device playing the same file would fight over its position — and the
                    // previous device has to be released either way: every call used to leak a
                    // WaveOut handle plus its playback thread.
                    DisposeActiveDevice();

                    var outputDevice = new WaveOutEvent();
                    try
                    {
                        outputDevice.PlaybackStopped += (_, __) => OnPlaybackStopped(outputDevice);
                        audioFile.Position = 0;
                        outputDevice.Init(audioFile);
                        outputDevice.Volume = (float)currentVolume;
                        outputDevice.Play();
                    }
                    catch
                    {
                        // Nothing is playing, so nothing will raise PlaybackStopped to release it.
                        // Without this, an unavailable audio device leaks one handle per press.
                        DisposeDevice(outputDevice);
                        throw;
                    }

                    _activeDevice = outputDevice;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error while playing sound {sound.ConvertToString()}.");
            }
        }

        private void OnPlaybackStopped(WaveOutEvent device)
        {
            lock (_playbackLock)
            {
                // A newer sound has already taken over and disposed this device.
                if (!ReferenceEquals(_activeDevice, device))
                    return;

                _activeDevice = null;
            }

            DisposeDevice(device);
        }

        /// <summary>Caller holds <see cref="_playbackLock"/>.</summary>
        private void DisposeActiveDevice()
        {
            var device = _activeDevice;
            _activeDevice = null;

            if (device != null)
                DisposeDevice(device);
        }

        private void DisposeDevice(WaveOutEvent device)
        {
            try
            {
                // Stops playback as well.
                device.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while releasing the audio output device.");
            }
        }
    }

    public enum Sound
    {
        Unknown,
        CaptureStarted,
        CaptureStopped,
        NoProcess,
        MoreThanOneProcess
    }

    public enum SoundMode
    {
        None,
        Simple,
        Voice
    }
}
