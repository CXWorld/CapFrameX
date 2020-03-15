using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Interfaces;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Squidex.ClientLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Webservice.Implementation.Services
{
	public class SquidexService : IProcessListService, ISessionService
	{
		private readonly SquidexClientManager _squidexClientManager;
		public SquidexService(IOptions<SquidexOptions> squidexOptions)
		{
			_squidexClientManager = new SquidexClientManager(squidexOptions.Value);
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
				var result = await client.CreateAsync(sessionCollection, true);
				return result.Id;
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
