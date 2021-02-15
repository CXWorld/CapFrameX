using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Interfaces;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Squidex.ClientLibrary;
using Squidex.ClientLibrary.Management;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CapFrameX.Webservice.Data.Extensions;
using CapFrameX.Data.Session.Classes;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Webservice.Implementation.Handlers;

namespace CapFrameX.Webservice.Implementation.Services
{
    public class SquidexService : IProcessListService, ISessionService, ICrashlogReportingService
    {
        private readonly SquidexClientManager _squidexClientManager;
        private readonly SquidexOptions _squidexOptions;
        public SquidexService(IOptions<SquidexOptions> squidexOptions)
        {
            _squidexClientManager = new SquidexClientManager(squidexOptions.Value);
            _squidexOptions = squidexOptions.Value;
        }

        public async Task<IEnumerable<ProcessListData>> GetProcessList()
        {
            var client = _squidexClientManager.CreateContentsClient<ProcessList, ProcessListData>("processlist");
            using ((IDisposable)client)
            {
                var processList = new List<ProcessListData>();
                await client.GetAllAsync(100, data =>
                {
                    processList.Add(data.Data);
                    return Task.CompletedTask;
                });
                return processList;
            }
        }

        public async Task AddProcess(ProcessListData data)
        {
            var client = _squidexClientManager.CreateContentsClient<ProcessList, ProcessListData>("processlist");
            using ((IDisposable)client)
            {
                await client.CreateAsync(data);
            }
        }

        public async Task<SqSessionCollection> GetSessionCollection(Guid id)
        {
            var client = _squidexClientManager.CreateContentsClient<SqSessionCollection, SqSessionCollectionData>("sessioncollections");
            using ((IDisposable)client)
            {
                var collections = await client.GetAsync(new ContentQuery()
                {
                    Ids = new HashSet<string>() { id.ToString() }
                });
                return collections.Items.FirstOrDefault();
            }
        }

        public async Task<Guid> UploadAsset(byte[] data, string fileName)
        {
            var SESSIONFOLDERID = Guid.Parse("94c0cfae-19fc-4c64-be67-13683c04fe0a");
            var client = _squidexClientManager.CreateAssetsClient();
            var uploadedAsset = await client.PostAssetAsync(_squidexOptions.AppName, new FileParameter(new MemoryStream(data), fileName, "application/json"), SESSIONFOLDERID);
            return uploadedAsset.Id;
        }

        public async Task<Guid> UploadCrashlog(byte[] data, string filename)
        {
            var LOGFOLDERID = Guid.Parse("df35dedd-94e1-4fbd-a0a4-fc1a965ba0a5");
            var client = _squidexClientManager.CreateAssetsClient();
            var uploadedCrashlog = await client.PostAssetAsync(_squidexOptions.AppName, new FileParameter(new MemoryStream(data), filename, "application/json"), LOGFOLDERID);
            return uploadedCrashlog.Id;
        }

        public async Task<(string, byte[])> DownloadAsset(Guid id)
        {
            var client = _squidexClientManager.CreateAssetsClient();
            var asset = await client.GetAssetAsync(_squidexOptions.AppName, id.ToString());
            var assetContent = await client.GetAssetContentAsync(id.ToString());
            using var stream = new MemoryStream();
            await assetContent.Stream.CopyToAsync(stream);
            return (asset.FileName, stream.ToArray());
        }

        public async Task<IEnumerable<SqSessionCollection>> GetSessionCollectionsForUser(Guid userId)
        {
            var client = _squidexClientManager.CreateContentsClient<SqSessionCollection, SqSessionCollectionData>("sessioncollections");
            using ((IDisposable)client)
            {
                var collections = await client.GetAsync(new ContentQuery()
                {
                    JsonQuery = new
                    {
                        filter = new
                        {
                            path = "data.sub.iv",
                            op = "eq",
                            value = userId.ToString()
                        }
                    }
                });
                return collections.Items;
            }
        }

        public async Task<Guid> SaveSessionCollection(SqSessionCollectionData sessionCollection)
        {
            var client = _squidexClientManager.CreateContentsClient<SqSessionCollection, SqSessionCollectionData>("sessioncollections");
            using ((IDisposable)client)
            {
                try
                {
                    var result = await client.CreateAsync(sessionCollection, true);
                    return result.Id;
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }

        public async Task DeleteCollection(Guid id, Guid userId)
        {
            var client = _squidexClientManager.CreateContentsClient<SqSessionCollection, SqSessionCollectionData>("sessioncollections");
            using ((IDisposable)client)
            {
                var sessionCollection = await client.GetAsync(new ContentQuery()
                {
                    JsonQuery = new
                    {
                        filter = new
                        {
                            and = new object[] {
                                new {
                                    path = "data.sub.iv",
                                    op = "eq",
                                    value = userId.ToString()
                                },
                                new {
                                    path = "id",
                                    op = "eq",
                                    value = id.ToString()
                                }
                            }
                        }
                    }
                });
                if (!sessionCollection.Items.Any())
                {
                    throw new UnauthorizedAccessException("You cannot delete this session.");
                }
                await client.DeleteAsync(id);
            }
        }

        public async Task<IEnumerable<SqSessionData>> SearchSessions(string cpu, string gpu, string mainboard, string ram, string gameName, string comment)
        {
            var client = _squidexClientManager.CreateContentsClient<SqSessionCollection, SqSessionCollectionData>("sessioncollections");
            using ((IDisposable)client)
            {
                var filter = new List<string>();
                if (!string.IsNullOrWhiteSpace(cpu)) filter.Add($"contains(data/sessions/iv/cpu, '{cpu}')");
                if (!string.IsNullOrWhiteSpace(gpu)) filter.Add($"contains(data/sessions/iv/gpu, '{gpu}')");
                if (!string.IsNullOrWhiteSpace(ram)) filter.Add($"contains(data/sessions/iv/ram, '{ram}')");
                if (!string.IsNullOrWhiteSpace(mainboard)) filter.Add($"contains(data/sessions/iv/mainboard, '{mainboard}')");
                if (!string.IsNullOrWhiteSpace(gameName)) filter.Add($"contains(data/sessions/iv/gameName, '{gameName}')");
                if (!string.IsNullOrWhiteSpace(comment)) filter.Add($"contains(data/sessions/iv/comment, '{comment}')");
                var sessionCollectionResponse = await client.GetAsync(new ContentQuery()
                {
                    Filter = string.Join(" and ", filter)
                });

                Func<string, string, bool> checkContainsString = (value, searchTerm) => string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(searchTerm) || value.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) > -1;
                Func<SqSessionData, bool> sessionMatchesFilter = (SqSessionData session) =>
                       checkContainsString(session.Cpu, cpu)
                    && checkContainsString(session.Gpu, gpu)
                    && checkContainsString(session.GameName, gameName)
                    && checkContainsString(session.Mainboard, mainboard)
                    && checkContainsString(session.Ram, ram)
                    && checkContainsString(session.Comment, comment);

                return sessionCollectionResponse.Items.SelectMany(collection => collection.Data.Sessions.Where(sessionMatchesFilter)).OrderByDescending(x => x.CreationDate);
            }
        }

        private async Task UpdateMissingModelData(ContentsResult<SqSessionCollection, SqSessionCollectionData> sessionCollectionResponse)
        {
            var client2 = _squidexClientManager.CreateContentsClient<SqSessionCollection, SqSessionCollectionData>("sessioncollections");
            using ((IDisposable)client2)
            {
                foreach (var collection in sessionCollectionResponse.Items)
                {
                    if (collection.Data.Sessions.Any(s => string.IsNullOrWhiteSpace(s.Cpu)))
                    {
                        foreach (var session in collection.Data.Sessions)
                        {
                            if (session.File.FirstOrDefault() != null)
                            {
                                string fileName;
                                byte[] fileBytes;
                                (fileName, fileBytes) = await DownloadAsset(new Guid(session.File.First()));
                                if (fileName.EndsWith(".gz"))
                                {
                                    fileBytes = fileBytes.Decompress();
                                }
                                var sessionFromJson = JsonConvert.DeserializeObject<Session>(Encoding.UTF8.GetString(fileBytes));
                                session.GameName = sessionFromJson.Info.GameName;
                                session.Cpu = sessionFromJson.Info.Processor;
                                session.Gpu = sessionFromJson.Info.GPU;
                                session.Ram = sessionFromJson.Info.SystemRam;
                                session.Mainboard = sessionFromJson.Info.Motherboard;

                                IFrametimeStatisticProviderOptions _frametimeStatisticProviderOptions = new FrametimeStatisticProviderOptions()
                                {
                                    MovingAverageWindowSize = 500,
                                    IntervalAverageWindowTime = 500,
                                    FpsValuesRoundingDigits = 2
                                };
                                FrametimeStatisticProvider _frametimeStatisticProvider = new FrametimeStatisticProvider(_frametimeStatisticProviderOptions);
                                var frametimes = sessionFromJson.GetFrametimeTimeWindow(0, 1000, _frametimeStatisticProviderOptions);

                                session.AverageFps = _frametimeStatisticProvider.GetFpsMetricValue(frametimes, EMetric.Average);
                                session.P1Fps = _frametimeStatisticProvider.GetFpsMetricValue(frametimes, EMetric.P1);
                            }
                        }
                        await client2.UpdateAsync(collection.Id, collection.Data);
                    }
                }
            }
        }
    }
}
