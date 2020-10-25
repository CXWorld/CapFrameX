using CapFrameX.Contracts.Configuration;
using CapFrameX.Extensions;
using CapFrameX.Extensions.NetStandard;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace CapFrameX.Data
{
    public class SoundManager
    {
        private readonly MediaPlayer[] _soundPlayers = new MediaPlayer[6];
        private readonly Dictionary<string, int> _playerIndexDict = new Dictionary<string, int>(6);
        private readonly IAppConfiguration _configuration;

        public SoundMode SoundMode
        {
            get
            {
                return Enum.TryParse(_configuration.HotkeySoundMode, out SoundMode soundMode) ? soundMode : SoundMode.Voice;
            }
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

        public string[] AvailableSoundModes
        {
            get
            {
                return Enum.GetNames(typeof(SoundMode));
            }
        }

        public SoundManager(IAppConfiguration configuration)
        {
            _configuration = configuration;
            string soundPath;

            // capture started (voice)
            _soundPlayers[0] = new MediaPlayer();
            soundPath = Path.Combine("Sounds", SoundMode.Voice.ConvertToString(), $"{Sound.CaptureStarted.ConvertToString()}.mp3");
            _playerIndexDict.Add(soundPath, 0);
            _soundPlayers[0].Open(new Uri(soundPath, UriKind.Relative));

            // capture started (simple)
            _soundPlayers[1] = new MediaPlayer();
            soundPath = Path.Combine("Sounds", SoundMode.Simple.ConvertToString(), $"{Sound.CaptureStarted.ConvertToString()}.mp3");
            _playerIndexDict.Add(soundPath, 1);
            _soundPlayers[1].Open(new Uri(soundPath, UriKind.Relative));

            // capture stopped (voice)
            _soundPlayers[2] = new MediaPlayer();
            soundPath = Path.Combine("Sounds", SoundMode.Voice.ConvertToString(), $"{Sound.CaptureStopped.ConvertToString()}.mp3");
            _playerIndexDict.Add(soundPath, 2);
            _soundPlayers[2].Open(new Uri(soundPath, UriKind.Relative));

            // capture stopped (simple)
            _soundPlayers[3] = new MediaPlayer();
            soundPath = Path.Combine("Sounds", SoundMode.Simple.ConvertToString(), $"{Sound.CaptureStopped.ConvertToString()}.mp3");
            _playerIndexDict.Add(soundPath, 3);
            _soundPlayers[3].Open(new Uri(soundPath, UriKind.Relative));

            // more than one process (voice)
            _soundPlayers[4] = new MediaPlayer();
            soundPath = Path.Combine("Sounds", SoundMode.Voice.ConvertToString(), $"{Sound.MoreThanOneProcess.ConvertToString()}.mp3");
            _playerIndexDict.Add(soundPath, 4);
            _soundPlayers[4].Open(new Uri(soundPath, UriKind.Relative));

            // no process detected (voice)
            _soundPlayers[5] = new MediaPlayer();
            soundPath = Path.Combine("Sounds", SoundMode.Voice.ConvertToString(), $"{Sound.NoProcess.ConvertToString()}.mp3");
            _playerIndexDict.Add(soundPath, 5);
            _soundPlayers[5].Open(new Uri(soundPath, UriKind.Relative));
        }

        public void PlaySound(Sound sound)
        {
            if (SoundMode is SoundMode.None)
            {
                return;
            }

            var currentSoundMode = SoundMode;
            double currentVolume = Volume;
            Application.Current.Dispatcher.Invoke(() =>
            {
                var path = Path.Combine("Sounds", currentSoundMode.ConvertToString(), $"{sound.ConvertToString()}.mp3");
                var player = _soundPlayers[_playerIndexDict[path]];
                player.Volume = currentVolume;
                player.Play();
                player.Open(new Uri(path, UriKind.Relative));
            });
        }

        public void SetSoundMode(string value)
        {
            SoundMode = Enum.TryParse<SoundMode>(value, out var soundMode) ? soundMode : SoundMode.None;
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
