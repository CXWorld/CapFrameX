using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CapFrameX.Data
{
	public class ProcessList
	{
		private readonly List<CXProcess> _processList = new List<CXProcess>();
		private readonly string _filename;
		private readonly ISubject<int> _processListUpdate = new Subject<int>();
		public IObservable<int> ProcessesUpdate => _processListUpdate.AsObservable();

		public CXProcess[] Processes => _processList.ToArray();

		public ProcessList(string filename)
		{
			_filename = filename;
		}

		public string[] GetIgnoredProcessNames()
		{
			return Processes.Where(p => p.IsBlacklisted).Select(p => p.Name).OrderBy(p => p).ToArray();
		}

		public void AddEntry(string processName, string displayName, bool blacklist = false)
		{
			processName = processName.StripExeExtension();
			if (processName is null)
			{
				throw new ArgumentException(nameof(processName) + "is required");
			}
			if (_processList.Any(p => p.Name == processName))
			{
				return;
			}
			var process = new CXProcess(processName, displayName, blacklist, false);
			process.RegisterOnChange(() => _processListUpdate.OnNext(default));
			_processList.Add(process);
			_processListUpdate.OnNext(default);
		}

		public CXProcess FindProcessByProcessName(string processName)
		{
			processName = processName.StripExeExtension();
			var process = Processes.FirstOrDefault(p => p.Name == processName);
			return process;
		}

		public void Save()
		{
			var json = JsonConvert.SerializeObject(_processList.OrderBy(p => p.Name), Formatting.Indented);
			File.WriteAllText(_filename, json);
		}

		public void ReadFromFile()
		{
			var text = File.ReadAllText(_filename);
			_processList.Clear();

			var processes = JsonConvert.DeserializeObject<List<CXProcess>>(text);
			foreach (var proc in processes)
			{
				proc.RegisterOnChange(() => _processListUpdate.OnNext(default));
			}
			_processList.AddRange(processes);
			_processListUpdate.OnNext(default);
		}

		public static ProcessList Create(string filename)
		{
			ProcessList processList = new ProcessList(filename);
			try
			{
				processList.ReadFromFile();

				var defaultIgnorelistFileInfo = new FileInfo(Path.Combine("PresentMon", "ProcessIgnoreList.txt"));
				if (!defaultIgnorelistFileInfo.Exists)
				{
					return processList;
				}

				foreach (var process in File.ReadAllLines(defaultIgnorelistFileInfo.FullName))
				{
					processList.AddEntry(process, null, true);
				}
				processList.Save();
			}
			catch (Exception e)
			{

			}
			return processList;
		}
	}

	internal static class StringExtensions
	{
		internal static string StripExeExtension(this string processName)
		{
			return processName.Replace(".exe", string.Empty);
		}
	}

	public class CXProcess
	{
		private Action _onChange;
		[JsonProperty]
		public string Name { get; private set; }
		[JsonProperty]
		public string DisplayName { get; private set; }
		[JsonProperty]
		public bool IsWhitelisted { get; private set; }
		[JsonProperty]
		public bool IsBlacklisted { get; private set; }

		public CXProcess()
		{

		}

		public CXProcess(string name, string displayName, bool isBlacklisted, bool isWhitelisted)
		{
			Name = name;
			DisplayName = displayName;
			IsWhitelisted = isWhitelisted;
			IsBlacklisted = isBlacklisted;
		}

		public void Blacklist()
		{
			IsBlacklisted = true;
			IsWhitelisted = false;
			_onChange();
		}

		public void Whitelist()
		{
			IsWhitelisted = true;
			IsBlacklisted = false;
			_onChange();
		}

		public void UpdateDisplayName(string newName)
		{
			DisplayName = newName;
			_onChange();
		}

		public void RegisterOnChange(Action action)
		{
			_onChange = action;
		}
	}
}
