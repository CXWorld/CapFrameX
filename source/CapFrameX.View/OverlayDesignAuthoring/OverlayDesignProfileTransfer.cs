using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CapFrameX.Contracts.Localization;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Integration;

namespace CapFrameX.View.OverlayDesignAuthoring
{
    /// <summary>
    /// Tile designs as files. An export is the design as saved, with this machine's sensor IDs; the
    /// importing machine adapts it to its own hardware (<see cref="OverlayDesignImportMapper"/>).
    /// </summary>
    public static class OverlayDesignProfileTransfer
    {
        public static void Export(string designJson, string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            // Never write a file the import would reject.
            OsdDesignDocument.FromJson(designJson);
            // A failed export must leave a previous file intact.
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, designJson, new UTF8Encoding(false));
                File.Move(temporary, path, true);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        /// <summary>Reads a design file and adapts it to these sources; without sources it stays as it is.</summary>
        public static OverlayDesignImportResult Import(string path, IReadOnlyList<OverlayTelemetrySource> sources)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            if (new FileInfo(path).Length > OsdDesignDocument.MaximumJsonLength)
                throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_ImportSize"), message => new FormatException(message));
            var document = OsdDesignDocument.FromJson(File.ReadAllText(path));
            if (sources != null) return OverlayDesignImportMapper.Map(document, sources);
            return new OverlayDesignImportResult
            {
                Name = document.Name,
                DesignJson = document.ToJson(),
                IsGeneric = OverlayDesignImportMapper.IsGeneric(document),
                IsMapped = false
            };
        }
    }
}
