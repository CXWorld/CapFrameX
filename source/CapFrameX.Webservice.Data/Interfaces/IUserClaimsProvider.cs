using CapFrameX.Webservice.Data.DTO;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Data.Interfaces
{
	public interface IUserClaimsProvider
	{
		UserClaims GetUserClaims();
	}
}
