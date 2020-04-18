using CapFrameX.Webservice.Data.DTO;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Data.Queries
{
	public class GetSessionCollectionReducedByIdQuery: IRequest<SessionCollectionReducedDTO>
	{
		public Guid Id { get; set; }
	}
}
