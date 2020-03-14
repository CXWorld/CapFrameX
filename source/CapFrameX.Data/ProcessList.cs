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

		public void AddEntry(string processName)
		{
			if (_processList.Any(p => p.Name == processName))
			{
				return;
			}
			var process = new CXProcess(processName);
			process.RegisterOnChange(() => _processListUpdate.OnNext(default));
			_processList.Add(process);
			_processListUpdate.OnNext(default);
		}

		public void Save()
		{
			var json = JsonConvert.SerializeObject(_processList.OrderBy(p =>
			{
				if (p.IsBlacklisted)
					return 0;
				else if (p.IsWhitelisted)
					return 1;
				else
					return 2;
			})
			.ThenBy(p => p.Name), Formatting.Indented);
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
	}

	public class CXProcess
	{
		private Action _onChange;
		[JsonProperty]
		public string Name { get; private set; }
		[JsonProperty]
		public bool IsWhitelisted { get; private set; }
		[JsonProperty]
		public bool IsBlacklisted { get; private set; }

		public CXProcess() {
			
		}

		public CXProcess(string name, bool isBlacklisted, bool isWhitelisted)
		{
			Name = name;
			IsWhitelisted = isWhitelisted;
			IsBlacklisted = isBlacklisted;
		}

		public CXProcess(string name) : this(name, true, false) { }

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

		public void RegisterOnChange(Action action)
		{
			_onChange = action;
		}
	}
}
