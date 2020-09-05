using CapFrameX.Contracts.Configuration;
using CapFrameX.Extensions.NetStandard;
using System;
using System.IO;
using System.Windows.Media;

namespace CapFrameX.Data
{
    public class SoundManager
    {
        private readonly MediaPlayer _soundPlayer = new MediaPlayer();
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
        }

        public void PlaySound(Sound sound)
        {
            if (SoundMode is SoundMode.None)
            {
                return;
            }

            var path = Path.Combine("Sounds", SoundMode.ConvertToString(), $"{sound.ConvertToString()}.mp3");
            PlayFile(new Uri(path, UriKind.Relative), Volume);
        }

        private void PlayFile(Uri uri, double volume)
        {
            _soundPlayer.Open(uri);
            _soundPlayer.Volume = volume;
            _soundPlayer.Play();
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
