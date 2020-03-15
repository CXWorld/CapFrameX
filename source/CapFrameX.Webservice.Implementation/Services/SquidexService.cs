using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Interfaces;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Squidex.ClientLibrary;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Webservice.Implementation.Services
{
	public class SquidexService: ISquidexService
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
	}
}
