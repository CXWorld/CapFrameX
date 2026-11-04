using System;
using System.IO;
using System.Xml.Serialization;

namespace CapFrameX.CaptureHelper
{
    public sealed class Settings
    {
        public string Hotkey { get; set; } = HotkeyOption.All[0].Name;

        public int DurationSeconds { get; set; } = 20;

        public string CaptureFolder { get; set; } = DefaultCaptureFolder;

        public static string ConfigurationFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CapFrameX", "CaptureHelper");

        private static string FilePath => Path.Combine(ConfigurationFolder, "settings.xml");

        private static string DefaultCaptureFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CapFrameX Capture Helper");

        public static Settings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    using (var stream = File.OpenRead(FilePath))
                    {
                        var settings = (Settings)new XmlSerializer(typeof(Settings)).Deserialize(stream);
                        if (string.IsNullOrWhiteSpace(settings.CaptureFolder))
                            settings.CaptureFolder = DefaultCaptureFolder;
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write("Reading the settings failed, using the defaults: " + ex.Message);
            }
            return new Settings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(ConfigurationFolder);
                using (var stream = File.Create(FilePath))
                    new XmlSerializer(typeof(Settings)).Serialize(stream, this);
            }
            catch (Exception ex)
            {
                Log.Write("Saving the settings failed: " + ex.Message);
            }
        }
    }
}
