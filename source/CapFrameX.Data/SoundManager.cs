using CapFrameX.Contracts.Configuration;
using CapFrameX.Extensions.NetStandard;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace CapFrameX.Data
{
    public class SoundManager
    {
        private readonly Dictionary<string, MediaPlayer> _playerDictionary = new Dictionary<string, MediaPlayer>(6);
        private readonly IAppConfiguration _configuration;
        private readonly ILogger<SoundManager> _logger;

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
                    _playerDictionary.Add(path, new MediaPlayer());
                    _playerDictionary[path].Open(new Uri(path, UriKind.Relative));
                }
                catch(Exception ex)
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

            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    var path = Path.Combine("Sounds", currentSoundMode.ConvertToString(), $"{sound.ConvertToString()}.mp3");
                    var player = _playerDictionary[path];
                    player.Volume = currentVolume;
                    player.Play();
                    player.Open(new Uri(path, UriKind.Relative));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error while playing sound {sound.ConvertToString()}.");
                }
            });
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
