using CapFrameX.EventAggregation.Messages;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Data
{
	public class LoginManager
	{
		private readonly ILogger<LoginManager> _logger;
		private readonly PubSubEvent<AppMessages.LoginState> _loginStateEvent;

		public OAuthState State { get; } = new OAuthState();
		private OAuthRequest Request { get; set; } = OAuthRequest.BuildLoopbackRequest(new string[] { "profile", "offline_access" });

		public LoginManager(ILogger<LoginManager> logger, IEventAggregator eventAggregator)
		{
			_logger = logger;
			_loginStateEvent = eventAggregator.GetEvent<PubSubEvent<AppMessages.LoginState>>();
			Initialize();
		}

		public void Initialize()
		{
			var fileInfo = new FileInfo("OAuthState.dat");
			if (fileInfo.Exists)
			{
				State.Token = OAuthToken.FromJson(File.ReadAllText(fileInfo.FullName));
			}
			Task.Run(() => RefreshTokenIfNeeded());
		}

		public async Task Logout()
		{
			ApplyToken(null);
		}

		public async Task HandleRedirect(Func<string, Task> navigateAction)
		{
			State.Token = null;
			using (var listener = new HttpListener())
			{
				listener.Prefixes.Add(Request.RedirectUri);
				listener.Start();

				await navigateAction(Request.AuthorizationRequestUri);

				var context = await listener.GetContextAsync();

				string html = string.Format("<html><body></body></html>");
				var buffer = Encoding.UTF8.GetBytes(html);
				context.Response.ContentLength64 = buffer.Length;
				using (var stream = context.Response.OutputStream)
				{
					var responseTask = stream.WriteAsync(buffer, 0, buffer.Length);
				}

				string error = context.Request.QueryString["error"];
				if (error != null)
					return;

				string state = context.Request.QueryString["state"];
				if (state != Request.State)
					return;

				string code = context.Request.QueryString["code"];
				_logger.LogInformation("Login successfull");
				ApplyToken(await Request.ExchangeCodeForAccessToken(code));
				_logger.LogDebug("Token information: {@token}", State.Token);
			}
		}

		public async Task RefreshTokenIfNeeded()
		{
			if(State.Token != null && State.IsNotSigned)
			{
				await RefreshToken();
			}
		}

		public void EnableTokenRefresh(CancellationToken cancellationToken)
		{
			Observable.Timer(State.Token.ExpirationDate.AddSeconds(-30))
				.SelectMany(_ => Observable.Concat(Observable.Return(1L), Observable.Interval(TimeSpan.FromSeconds(State.Token.ExpiresIn - 30)))
									.SelectMany(index =>
									{
										_logger.LogInformation("Refreshing Accesstoken");
										return Request.Refresh(State.Token);
									})
							)
				.TakeWhile(_ => State.Token != null && !cancellationToken.IsCancellationRequested)
				.Subscribe(token =>
				{
					ApplyToken(token);
				});
		}

		private void ApplyToken(OAuthToken token)
		{
			State.Token = token;
			var fileInfo = new FileInfo("OAuthState.dat");
			if (State.Token is null)
			{
				_loginStateEvent.Publish(new AppMessages.LoginState(false));
				File.Delete(fileInfo.FullName);
			} else
			{
				_loginStateEvent.Publish(new AppMessages.LoginState(true));
				File.WriteAllText(fileInfo.FullName, token.ToJson());
			}
		}

		private async Task RefreshToken()
		{
			ApplyToken(await Request.Refresh(State.Token));
		}
	}

	public class OAuthState : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;

		private OAuthToken _token;
		public OAuthToken Token
		{
			get => _token;
			set
			{
				if (_token == value)
					return;

				_token = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Token)));
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSigned)));
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsNotSigned)));
			}
		}

		public bool IsSigned => Token != null && Token.ExpirationDate > DateTime.Now;
		public bool IsNotSigned => !IsSigned;
	}

	public class OAuthToken
	{
		[JsonProperty("access_token")]
		public string AccessToken { get; set; }

		[JsonProperty("token_type")]
		public string TokenType { get; set; }

		[JsonProperty("expires_in")]
		public int ExpiresIn { get; set; }

		[JsonProperty("refresh_token")]
		public string RefreshToken { get; set; }

		public string Name { get; set; }

		public string Email { get; set; }

		public string Picture { get; set; }

		public string Locale { get; set; }

		public string FamilyName { get; set; }

		public string GivenName { get; set; }

		public string Id { get; set; }

		public string Profile { get; set; }

		public string[] Scopes { get; set; }

		public DateTime ExpirationDate { get; set; }

		public string ToJson()
		{
			return JsonConvert.SerializeObject(this, new JsonSerializerSettings()
			{
				DateFormatString = "yyyy-MM-dd'T'HH:mm:ss.fffK"
			});
		}

		public static OAuthToken FromJson(string json)
		{
			return JsonConvert.DeserializeObject<OAuthToken>(json, new JsonSerializerSettings()
			{
				DateFormatString = "yyyy-MM-dd'T'HH:mm:ss.fffK"
			});
		}
	}

	public sealed class OAuthRequest
	{
		private const string _clientId = "application";
		private const string _clientSecret = "13e2d80b-32e7-4d48-b2fe-29c44bca7958";

		private const string _authorizationEndpoint = "https://capframex.com/auth/realms/CapFrameX/protocol/openid-connect/auth";
		private const string _tokenEndpoint = "https://capframex.com/auth/realms/CapFrameX/protocol/openid-connect/token";
		private const string _userInfoEndpoint = "https://capframex.com/auth/realms/CapFrameX/protocol/openid-connect/userinfo";

		private OAuthRequest()
		{
		}

		public string AuthorizationRequestUri { get; private set; }
		public string State { get; private set; }
		public string RedirectUri { get; private set; }
		public string CodeVerifier { get; private set; }
		public string[] Scopes { get; private set; }

		public static OAuthRequest BuildLoopbackRequest(params string[] scopes)
		{
			var request = new OAuthRequest
			{
				CodeVerifier = RandomDataBase64Url(32),
				Scopes = scopes
			};

			string codeChallenge = Base64UrlEncodeNoPadding(Sha256(request.CodeVerifier));
			const string codeChallengeMethod = "S256";

			string scope = BuildScopes(scopes);

			request.RedirectUri = string.Format("http://{0}:{1}/", IPAddress.Loopback, GetRandomUnusedPort());
			request.State = RandomDataBase64Url(32);
			request.AuthorizationRequestUri = string.Format("{0}?response_type=code&scope=openid%20profile{6}&redirect_uri={1}&client_id={2}&state={3}&code_challenge={4}&code_challenge_method={5}",
				_authorizationEndpoint,
				Uri.EscapeDataString(request.RedirectUri),
				_clientId,
				request.State,
				codeChallenge,
				codeChallengeMethod,
				scope);

			return request;
		}

		public Task<OAuthToken> ExchangeCodeForAccessToken(string code)
		{
			if (code == null)
				throw new ArgumentNullException(nameof(code));

			string tokenRequestBody = string.Format("code={0}&redirect_uri={1}&client_id={2}&code_verifier={3}&client_secret={4}&scope=&grant_type=authorization_code",
				code,
				Uri.EscapeDataString(RedirectUri),
				_clientId,
				CodeVerifier,
				_clientSecret
				);

			return TokenRequest(tokenRequestBody, Scopes);
		}

		public Task<OAuthToken> Refresh(OAuthToken oldToken)
		{
			if (oldToken == null)
				throw new ArgumentNullException(nameof(oldToken));
			try
			{
				string tokenRequestBody = string.Format("refresh_token={0}&client_id={1}&client_secret={2}&grant_type=refresh_token",
					oldToken.RefreshToken,
					_clientId,
					_clientSecret
					);

				return TokenRequest(tokenRequestBody, oldToken.Scopes);
			} catch(Exception e)
			{
				return null;
			}
		}

		private static int GetRandomUnusedPort()
		{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			var port = ((IPEndPoint)listener.LocalEndpoint).Port;
			listener.Stop();
			return port;
		}

		private static string RandomDataBase64Url(int length)
		{
			using (var rng = new RNGCryptoServiceProvider())
			{
				var bytes = new byte[length];
				rng.GetBytes(bytes);
				return Base64UrlEncodeNoPadding(bytes);
			}
		}

		private static byte[] Sha256(string text)
		{
			using (var sha256 = new SHA256Managed())
			{
				return sha256.ComputeHash(Encoding.ASCII.GetBytes(text));
			}
		}

		private static string Base64UrlEncodeNoPadding(byte[] buffer)
		{
			string b64 = Convert.ToBase64String(buffer);
			// converts base64 to base64url.
			b64 = b64.Replace('+', '-');
			b64 = b64.Replace('/', '_');
			// strips padding.
			b64 = b64.Replace("=", "");
			return b64;
		}

		private static async Task<OAuthToken> TokenRequest(string tokenRequestBody, string[] scopes)
		{
			var request = (HttpWebRequest)WebRequest.Create(_tokenEndpoint);
			request.Method = "POST";
			request.ContentType = "application/x-www-form-urlencoded";
			byte[] bytes = Encoding.ASCII.GetBytes(tokenRequestBody);
			using (var requestStream = request.GetRequestStream())
			{
				requestStream.Write(bytes, 0, bytes.Length);
			}

			var response = await request.GetResponseAsync();
			using (var responseStream = response.GetResponseStream())
			{
				using (StreamReader sr = new StreamReader(responseStream))
				{
					var token = JsonConvert.DeserializeObject<OAuthToken>(sr.ReadToEnd());
					token.ExpirationDate = DateTime.Now + new TimeSpan(0, 0, token.ExpiresIn);
					var user = GetUserInfo(token.AccessToken);
					token.Name = user.Name;
					token.Picture = user.Picture;
					token.Email = user.Email;
					token.Locale = user.Locale;
					token.FamilyName = user.FamilyName;
					token.GivenName = user.GivenName;
					token.Id = user.Id;
					token.Profile = user.Profile;
					token.Scopes = scopes;
					return token;
				}
			}
		}

		private static UserInfo GetUserInfo(string accessToken)
		{
			var request = (HttpWebRequest)WebRequest.Create(_userInfoEndpoint);
			request.Method = "GET";
			request.Headers.Add(string.Format("Authorization: Bearer {0}", accessToken));
			var response = request.GetResponse();
			using (var responseStream = response.GetResponseStream())
			{
				using (var reader = new StreamReader(responseStream))
				{
					return JsonConvert.DeserializeObject<UserInfo>(reader.ReadToEnd());
				}
			}
		}

		private static string BuildScopes(string[] scopes)
		{
			string scope = null;
			if (scopes != null)
			{
				foreach (var sc in scopes)
				{
					scope += "%20" + Uri.EscapeDataString(sc);
				}
			}
			return scope;
		}

		private class UserInfo
		{
			[JsonProperty("name")]
			public string Name { get; set; }

			[JsonProperty("kind")]
			public string Kind { get; set; }

			[JsonProperty("email")]
			public string Email { get; set; }

			[JsonProperty("picture")]
			public string Picture { get; set; }

			[JsonProperty("locale")]
			public string Locale { get; set; }

			[JsonProperty("family_name")]
			public string FamilyName { get; set; }

			[JsonProperty("given_name")]
			public string GivenName { get; set; }

			[JsonProperty("sub")]
			public string Id { get; set; }

			[JsonProperty("profile")]
			public string Profile { get; set; }

			[JsonProperty("gender")]
			public string Gender { get; set; }
		}
	}
}
