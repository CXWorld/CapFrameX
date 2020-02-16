using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Interfaces;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace CapFrameX.Webservice.Data.Providers
{
	public class UserClaimsProvider : IUserClaimsProvider
	{
		private readonly IHttpContextAccessor _httpContextAccessor;

		public UserClaimsProvider(IHttpContextAccessor httpContextAccessor)
		{
			_httpContextAccessor = httpContextAccessor;
		}
		public UserClaims GetUserClaims()
		{
			var claimsPrincipal = _httpContextAccessor.HttpContext.User;

			if (_httpContextAccessor.HttpContext.User is null)
			{
				return null;
			}
			var claims = new UserClaims()
			{
				Sub = Guid.Parse(claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier).Value),
				Email = claimsPrincipal.FindFirst(ClaimTypes.Email).Value,
				EmailVerified = bool.Parse(claimsPrincipal.FindFirst("email_verified").Value),
				GivenName = claimsPrincipal.FindFirst(ClaimTypes.GivenName).Value,
				FamilyName = claimsPrincipal.FindFirst(ClaimTypes.Surname).Value
			};
			return claims;
		}
	}
}
