using CapFrameX.Contracts.Configuration;
using CapFrameX.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace CapFrameX.Data
{
    public class ProcessList
    {
        private readonly List<CXProcess> _processList = new List<CXProcess>();
        private readonly string _filename;
        private readonly IAppConfiguration _appConfiguration;
        private readonly ILogger<ProcessList> _logger;
        private readonly ISubject<int> _processListUpdate = new Subject<int>();
        private bool ProcesslistInitialized { get; set; }
        public IObservable<int> ProcessesUpdate => _processListUpdate.AsObservable();

        public CXProcess[] Processes => _processList.ToArray();

        private ProcessList(string filename,
            IAppConfiguration appConfiguration,
            ILogger<ProcessList> logger)
        {
            _filename = filename;
            _appConfiguration = appConfiguration;
            _logger = logger;
        }

        public string[] GetIgnoredProcessNames()
        {
            return Processes.Where(p => p.IsBlacklisted).Select(p => p.Name).OrderBy(p => p).ToArray();
        }

        public void AddEntry(string processName, string displayName, bool blacklist = false, double? lastCaptureTime = null)
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

            var process = new CXProcess(processName, displayName, blacklist, lastCaptureTime);
            process.RegisterOnChange(() => _processListUpdate.OnNext(default));
            _processList.Add(process);
            _processListUpdate.OnNext(default);

            if (blacklist || displayName != null)
            {
                UploadProcessInfo(processName, displayName, blacklist);
            }
        }


        public void UploadProcessInfo(string processName, string displayName, bool blacklist = false)
        {
            if (_appConfiguration.ShareProcessListEntries && ProcesslistInitialized)
            {
                Task.Run(async () =>
                {
                    using (var client = new HttpClient()
                    {
                        BaseAddress = new Uri(ConfigurationManager.AppSettings["WebserviceUri"])
                    })
                    {
                        try
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
                        catch(Exception ex)
                        {
                            _logger.LogError(ex, "Error while uploading process info.");
                        }
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

        public async Task Save()
        {
            var json = JsonConvert.SerializeObject(_processList.OrderBy(p => p.Name), Formatting.Indented);

            try
            {
                using (StreamWriter outputFile = new StreamWriter(_filename))
                {
                    await outputFile.WriteAsync(json);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while saving process list.");
            }
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
                    AddEntry(proc.Name, proc.DisplayName, proc.IsBlacklisted, proc.LastCaptureTime);
                }

                await Save();
            }
        }

        public static ProcessList Create(string filename, string foldername, IAppConfiguration appConfiguration, ILogger<ProcessList> logger)
        {
            if (!Directory.Exists(foldername)) Directory.CreateDirectory(foldername);
            var fullpath = Path.Combine(foldername, filename);
            ProcessList processList = new ProcessList(fullpath, appConfiguration, logger);
            Task.Run(async () =>
            {
                try
                {
                    try
                    {
                        processList.ReadFromFile();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error while reading from file.");
                    }

                    var defaultProcesslistFileInfo = new FileInfo(Path.Combine("ProcessList", "Processes.json"));
                    if (!defaultProcesslistFileInfo.Exists)
                    {
                        return;
                    }

                    var defaultProcesses = JsonConvert.DeserializeObject<List<CXProcess>>(File.ReadAllText(defaultProcesslistFileInfo.FullName));
                    foreach (var process in defaultProcesses)
                    {
                        processList.AddEntry(process.Name, process.DisplayName, process.IsBlacklisted, process.LastCaptureTime);
                    }
                    await processList.Save();

                    if (appConfiguration.AutoUpdateProcessList)
                    {
                        processList.ProcesslistInitialized = false;
                        await UpdateProcessListAsync(processList);
                    }
                    else
                        processList.ProcesslistInitialized = true;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while creating process list.");
                }
                finally
                {
                    processList.ProcesslistInitialized = true;
                }
            });

            return processList;
        }

        private static Task<ConfiguredTaskAwaitable> UpdateProcessListAsync(ProcessList processList)
        {
            return Task.Run(() => processList.UpdateProcessListFromWebserviceAsync().ContinueWith(t =>
            {
                processList.ProcesslistInitialized = true;
            }).ConfigureAwait(false));
        }
    }

    public static class StringExtensions
    {
        public static string StripExeExtension(this string processName)
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
        [JsonProperty]
        public double? LastCaptureTime { get; private set; }

        [JsonConstructor]
        public CXProcess() { }

        public CXProcess(string name, string displayName, bool isBlacklisted, double? lastCaptureTime)
        {
            Name = name;
            DisplayName = displayName;
            IsBlacklisted = isBlacklisted;
            LastCaptureTime = lastCaptureTime;
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

        public void UpdateCaptureTime(double lastCaptureTime)
        {
            LastCaptureTime = lastCaptureTime;
            _onChange();
        }

        public void RegisterOnChange(Action action)
        {
            _onChange = action;
        }
    }
}