using CapFrameX.Webservice.Data.Entities;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Webservice.Data.Interfaces
{
	public interface IGameListService
	{
		Task<Game[]> GetGameListAsync();
	}
}
