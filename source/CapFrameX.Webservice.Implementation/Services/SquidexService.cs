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

namespace CapFrameX.Webservice.Implementation.Services
{
	public class SquidexService : IProcessListService, ISessionService
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
				var uploadedAsset = await client.PostAssetAsync(_squidexOptions.AppName, new FileParameter(new MemoryStream(data), fileName, "application/json"));
				return uploadedAsset.Id;
		}

		public async Task<byte[]> DownloadAsset(Guid id)
		{
			var client = _squidexClientManager.CreateAssetsClient();
			var asset = await client.GetAssetContentAsync(id.ToString());
			using var stream = new MemoryStream();
			await asset.Stream.CopyToAsync(stream);
			return stream.ToArray();
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
				} catch(Exception e)
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
	}
}
