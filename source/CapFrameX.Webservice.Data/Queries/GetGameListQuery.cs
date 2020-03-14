using CapFrameX.Webservice.Data.Entities;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Data.Queries
{
	public class GetGameListQuery: IRequest<Game[]>
	{
	}
}
