using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using CapFrameX.Configuration;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Extensions;
using Newtonsoft.Json;

namespace CapFrameX.Data
{
	public class ProcessList
	{
		private readonly List<CXProcess> _processList = new List<CXProcess>();
		private readonly string _filename;
		private readonly IAppConfiguration _appConfiguration;
		private readonly ISubject<int> _processListUpdate = new Subject<int>();
		private bool ProcesslistInitialized { get; set; }
		public IObservable<int> ProcessesUpdate => _processListUpdate.AsObservable();

		public CXProcess[] Processes => _processList.ToArray();

		private ProcessList(string filename, IAppConfiguration appConfiguration)
		{
			_filename = filename;
			_appConfiguration = appConfiguration;
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
			var process = new CXProcess(processName, displayName, blacklist);
			process.RegisterOnChange(() => _processListUpdate.OnNext(default));
			_processList.Add(process);
			_processListUpdate.OnNext(default);

			if (_appConfiguration.ShareProcessListEntries && ProcesslistInitialized)
			{
				Task.Run(async () =>
				{
					using (var client = new HttpClient()
					{
						BaseAddress = new Uri(ConfigurationManager.AppSettings["WebserviceUri"])
					})
					{
						client.DefaultRequestHeaders.AddCXClientUserAgent();
						var content = new StringContent(JsonConvert.SerializeObject(new
						{
							Name = processName,
							DisplayName = displayName,
							IsBlacklisted = blacklist
						}));
						content.Headers.ContentType.MediaType = "application/json";
						var response = await client.PostAsync("ProcessList", content);
					}
				});
			}
		}

		public CXProcess FindProcessByName(string processName)
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
					AddEntry(proc.Name, proc.DisplayName, proc.IsBlacklisted);
				}
				Save();
			}
		}

		public static ProcessList Create(string filename, IAppConfiguration appConfiguration)
		{
			ProcessList processList = new ProcessList(filename, appConfiguration);
			Task.Run(() =>
			{
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
						return;
					}

					var defaultProcesses = JsonConvert.DeserializeObject<List<CXProcess>>(File.ReadAllText(defaultProcesslistFileInfo.FullName));
					foreach (var process in defaultProcesses)
					{
						processList.AddEntry(process.Name, process.DisplayName, process.IsBlacklisted);
					}
					processList.Save();

					if (appConfiguration.AutoUpdateProcessList)
					{
						processList.ProcesslistInitialized = false;
						Task.Run(() => processList.UpdateProcessListFromWebserviceAsync().ContinueWith(t =>
						{
							processList.ProcesslistInitialized = true;
						}).ConfigureAwait(false));
					}
					else
						processList.ProcesslistInitialized = true;
				}
				catch (Exception)
				{
					//ToDo: logging!
				}
				finally
				{
					processList.ProcesslistInitialized = true;
				}
			});
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
		public bool IsBlacklisted { get; private set; }

		[JsonConstructor]
		public CXProcess() { }

		public CXProcess(string name, string displayName, bool isBlacklisted)
		{
			Name = name;
			DisplayName = displayName;
			IsBlacklisted = isBlacklisted;
		}

		public void Blacklist()
		{
			IsBlacklisted = true;
			_onChange();
		}

		public void Whitelist()
		{
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