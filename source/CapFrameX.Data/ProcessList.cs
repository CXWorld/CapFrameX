using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using CapFrameX.Extensions;
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

		public void AddEntry(string processName, string displayName, bool blacklist = false, bool whitelist = false)
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
			var process = new CXProcess(processName, displayName, blacklist, whitelist);
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

		public async Task UpdateProcessListFromWebserviceAsync()
		{
			using (var client = new HttpClient()
			{
				BaseAddress = new Uri(ConfigurationManager.AppSettings["WebserviceUri"])
			})
			{
				client.DefaultRequestHeaders.AddCXClientUserAgent();
				var content = await client.GetStringAsync("ProcessList");
				var processes = JsonConvert.DeserializeObject<List<CXProcess>>(content);

				foreach (var proc in processes)
				{
					AddEntry(proc.Name, proc.DisplayName, proc.IsBlacklisted, proc.IsWhitelisted);
				}
				Save();
			}
		}

		public static ProcessList Create(string filename)
		{
			ProcessList processList = new ProcessList(filename);
			try
			{
				try
				{
					processList.ReadFromFile();

				}
				catch { }
				var defaultProcesslistFileInfo = new FileInfo(Path.Combine("ProcessList", "Processes.json"));
				if (!defaultProcesslistFileInfo.Exists)
				{
					return processList;
				}

				var defaultProcesses = JsonConvert.DeserializeObject<List<CXProcess>>(File.ReadAllText(defaultProcesslistFileInfo.FullName));
				foreach (var process in defaultProcesses)
				{
					processList.AddEntry(process.Name, process.DisplayName, process.IsBlacklisted, process.IsWhitelisted);
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

		[JsonConstructor]
		public CXProcess() { }

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
