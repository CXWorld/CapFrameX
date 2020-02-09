using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Exceptions;
using CapFrameX.Webservice.Data.Interfaces;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Webservice.Implementation.CaptureStorages
{
	public class MongoDBStorage : ICaptureStorage
	{
		private readonly IMongoCollection<CaptureCollection> _captures;
		private readonly MongoDbStorageConfiguration _settings;

		public MongoDBStorage(MongoDbStorageConfiguration settings)
		{
			_settings = settings;
			_captures = GetCaptures();
		}
		public async Task<CaptureCollection> GetCaptureCollection(Guid collectionId)
		{
			var result = await _captures.Find(x => x.Id == collectionId).FirstOrDefaultAsync();
			if(result == null)
			{
				throw new CaptureNotFoundException($"CaptureCollection {collectionId} not found");
			}
			return result;
		}

		public async Task<CaptureCollection> SaveCaptureCollection(CaptureCollection captureCollection)
		{
			captureCollection.Id = Guid.NewGuid();
			await _captures.InsertOneAsync(captureCollection);
			return captureCollection;
		}

		private IMongoCollection<CaptureCollection> GetCaptures()
		{
			var client = new MongoClient(_settings.ConnectionString);
			var database = client.GetDatabase(_settings.DatabaseName);
			try
			{
				database.CreateCollection(_settings.CollectionName);
			} catch (MongoCommandException) { }
			return database.GetCollection<CaptureCollection>(_settings.CollectionName);
		}
	}

	public class MongoDbStorageConfiguration
	{
		public string ConnectionString { get; set; }
		public string DatabaseName { get; set; }
		public string CollectionName { get; set; }
	}
}
