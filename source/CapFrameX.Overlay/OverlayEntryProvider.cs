using CapFrameX.Contracts.Overlay;
using CapFrameX.Extensions;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace CapFrameX.Overlay
{
	public class OverlayEntryProvider : IOverlayEntryProvider
	{
		private const string JSON_FILE_NAME
			= @"OverlayConfiguration\OverlayEntryConfiguration.json";

		private readonly Dictionary<string, IOverlayEntry> _identifierOverlayEntryDict;
		private List<IOverlayEntry> _overlayEntries;

		public OverlayEntryProvider()
		{
			_identifierOverlayEntryDict = new Dictionary<string, IOverlayEntry>();

			try
			{
				LoadOverlayEntriesFromJson();
			}
			catch
			{
				SetOverlayEntryDefaults();
			}
		}

		public IOverlayEntry[] GetOverlayEntries()
		{
			return _overlayEntries.ToArray();
		}

		public IOverlayEntry GetOverlayEntry(string identifier)
		{
			return _identifierOverlayEntryDict[identifier];
		}

		public void MoveEntry(int sourceIndex, int targetIndex)
		{
			_overlayEntries.Move(sourceIndex, targetIndex);
		}

		public bool SaveOverlayEntriesToJson()
		{
			try
			{
				var persistence = new OverlayEntryPersistence()
				{
					OverlayEntries = _overlayEntries
				};

				var json = JsonConvert.SerializeObject(persistence);
				File.WriteAllText(JSON_FILE_NAME, json);

				return true;
			}
			catch { return false; }
		}

		private void LoadOverlayEntriesFromJson()
		{
			string json = File.ReadAllText(JSON_FILE_NAME);
			_overlayEntries = new List<IOverlayEntry>(JsonConvert.
				DeserializeObject<OverlayEntryPersistence>(json).OverlayEntries);

			foreach (var entry in _overlayEntries)
			{
				_identifierOverlayEntryDict.Add(entry.Identifier, entry);
			}
		}

		private void SetOverlayEntryDefaults()
		{
			_overlayEntries = new List<IOverlayEntry>
				{
					// CX 
					// CaptureServiceStatus
					new OverlayEntryWrapper("CaptureServiceStatus")
					{
						ShowOnOverlay = true,
						Description = "Capture service status",
						GroupName = string.Empty,
						Value = "Capture service ready...",
						ShowGraph = false,
						Color = string.Empty
					},

					// RunHistory
					new OverlayEntryWrapper("RunHistory")
					{
						ShowOnOverlay = true,
						Description = "Run history",
						GroupName = string.Empty,
						Value = default(object),
						ShowGraph = false,
						Color = string.Empty
					},

					// RunHistory
					new OverlayEntryWrapper("CaptureTimer")
					{
						ShowOnOverlay = true,
						Description = "Capture timer",
						GroupName = "Timer: ",
						Value = "0",
						ShowGraph = false,
						Color = string.Empty
					},

					// RTSS
					// Framerate
					new OverlayEntryWrapper("Framerate")
					{
						ShowOnOverlay = true,
						Description = "Framerate",
						GroupName = "<APP>",
						Value = 0d,
						ShowGraph = false,
						Color = string.Empty
					},

					// Frametime
					new OverlayEntryWrapper("Frametime")
					{
						ShowOnOverlay = true,
						Description = "Frametime",
						GroupName = "<APP>",
						Value = 0d,
						ShowGraph = true,
						Color = string.Empty
					}
				};

			foreach (var entry in _overlayEntries)
			{
				_identifierOverlayEntryDict.Add(entry.Identifier, entry);
			}
		}
	}
}
