using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Exceptions;
using CapFrameX.Webservice.Data.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Webservice.Implementation.CaptureStorages
{
	public class CaptureDiskStorage : ICaptureStorage
	{
		private readonly string _directory;

		public CaptureDiskStorage(string directory)
		{
			_directory = directory;
		}
		public async Task<CaptureCollection> GetCaptureCollection(Guid collectionId)
		{
			var directoryInfo = new DirectoryInfo(Path.Combine(_directory, collectionId.ToString()));
			if(!directoryInfo.Exists)
			{
				throw new CaptureNotFoundException($"Collection {collectionId} not found");
			}
			var captures = new List<Capture>();
			foreach(var fileInfo in directoryInfo.GetFiles())
			{
				captures.Add(new Capture()
				{
					CollectionId = collectionId,
					Name = fileInfo.Name,
					BlobBytes = await File.ReadAllBytesAsync(fileInfo.FullName)
				});
			}
			return new CaptureCollection()
			{
				Id = collectionId,
				Captures = captures,
				UploadTimestamp = directoryInfo.CreationTimeUtc
			};
		}

		public async Task<CaptureCollection> SaveCaptureCollection(CaptureCollection collection)
		{
			collection.Id = Guid.NewGuid();
			Directory.CreateDirectory(Path.Combine(_directory, collection.Id.ToString()));
			foreach(var capture in collection.Captures)
			{
				var fileInfo = new FileInfo(Path.Combine(_directory, collection.Id.ToString(), capture.Name));
				await File.WriteAllBytesAsync(fileInfo.FullName, capture.BlobBytes);
			}
			return collection;
		}
	}
}
