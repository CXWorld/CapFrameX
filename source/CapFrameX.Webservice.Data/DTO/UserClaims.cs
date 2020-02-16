using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Data.DTO
{
	public class UserClaims
	{
		public Guid Sub { get; set; }
		public string Email { get; set; }
		public string GivenName { get; set; }
		public string FamilyName { get; set; }
		public bool EmailVerified { get; set; }
	}
}
