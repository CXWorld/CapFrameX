using CapFrameX.Contracts.Data;
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
	public class CloudManager : ICloudManager
	{
		public LoginManager LoginManager { get; } = new LoginManager();
		public CloudManager() { }
	}

	public class LoginManager
	{

		public OAuthState State { get; } = new OAuthState();
		public OAuthRequest Request { get; private set; }

		public async Task HandleRedirect(Func<string, Task> navigateAction)
		{
			State.Token = null;

			// for example, let's pretend I want also want to have access to WebAlbums
			var scopes = new string[] { "profile", "offline_access" };

			Request = OAuthRequest.BuildLoopbackRequest(scopes);
			var listener = new HttpListener();
			listener.Prefixes.Add(Request.RedirectUri);
			listener.Start();

			// note: add a reference to System.Windows.Presentation and a 'using System.Windows.Threading' for this to compile
			await navigateAction(Request.AuthorizationRequestUri);

			// here, we'll wait for redirection from our hosted webbrowser
			var context = await listener.GetContextAsync();

			// browser has navigated to our small http servern answer anything here
			string html = string.Format("<html><body></body></html>");
			var buffer = Encoding.UTF8.GetBytes(html);
			context.Response.ContentLength64 = buffer.Length;
			var stream = context.Response.OutputStream;
			var responseTask = stream.WriteAsync(buffer, 0, buffer.Length).ContinueWith((task) =>
			{
				stream.Close();
				listener.Stop();
			});

			string error = context.Request.QueryString["error"];
			if (error != null)
				return;

			string state = context.Request.QueryString["state"];
			if (state != Request.State)
				return;

			string code = context.Request.QueryString["code"];
			State.Token = await Request.ExchangeCodeForAccessToken(code);
		}

		public void EnableTokenRefresh(CancellationToken cancellationToken)
		{
			Observable.Timer(State.Token.ExpirationDate.AddSeconds(-30))
				.SelectMany(_ => Observable.Concat(Observable.Return(1L), Observable.Interval(TimeSpan.FromSeconds(State.Token.ExpiresIn - 30)))
									.SelectMany(index => Request.Refresh(State.Token))
							)
				.TakeWhile(_ => State.Token != null && !cancellationToken.IsCancellationRequested)
				.Subscribe(token =>
				{
					State.Token = token;
				});
		}
	}

	// state model
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

	// This is a sample. Fille information (email, etc.) can depend on scopes
	[DataContract]
	public class OAuthToken
	{
		[DataMember(Name = "access_token")]
		public string AccessToken { get; set; }

		[DataMember(Name = "token_type")]
		public string TokenType { get; set; }

		[DataMember(Name = "expires_in")]
		public int ExpiresIn { get; set; }

		[DataMember(Name = "refresh_token")]
		public string RefreshToken { get; set; }

		[DataMember]
		public string Name { get; set; }

		[DataMember]
		public string Email { get; set; }

		[DataMember]
		public string Picture { get; set; }

		[DataMember]
		public string Locale { get; set; }

		[DataMember]
		public string FamilyName { get; set; }

		[DataMember]
		public string GivenName { get; set; }

		[DataMember]
		public string Id { get; set; }

		[DataMember]
		public string Profile { get; set; }

		[DataMember]
		public string[] Scopes { get; set; }

		// not from google's response, but we store this
		public DateTime ExpirationDate { get; set; }
	}

	// largely inspired from
	// https://github.com/googlesamples/oauth-apps-for-windows
	public sealed class OAuthRequest
	{
		// TODO: this is a sample, please change these two, use your own!
		private const string ClientId = "application";
		private const string ClientSecret = "13e2d80b-32e7-4d48-b2fe-29c44bca7958";

		private const string AuthorizationEndpoint = "https://capframex.com/auth/realms/CapFrameX/protocol/openid-connect/auth";
		private const string TokenEndpoint = "https://capframex.com/auth/realms/CapFrameX/protocol/openid-connect/token";
		private const string UserInfoEndpoint = "https://capframex.com/auth/realms/CapFrameX/protocol/openid-connect/userinfo";

		private OAuthRequest()
		{
		}

		public string AuthorizationRequestUri { get; private set; }
		public string State { get; private set; }
		public string RedirectUri { get; private set; }
		public string CodeVerifier { get; private set; }
		public string[] Scopes { get; private set; }

		// https://developers.google.com/identity/protocols/OAuth2InstalledApp
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
				AuthorizationEndpoint,
				Uri.EscapeDataString(request.RedirectUri),
				ClientId,
				request.State,
				codeChallenge,
				codeChallengeMethod,
				scope);

			return request;
		}

		// https://developers.google.com/identity/protocols/OAuth2InstalledApp Step 5: Exchange authorization code for refresh and access tokens
		public Task<OAuthToken> ExchangeCodeForAccessToken(string code)
		{
			if (code == null)
				throw new ArgumentNullException(nameof(code));

			string tokenRequestBody = string.Format("code={0}&redirect_uri={1}&client_id={2}&code_verifier={3}&client_secret={4}&scope=&grant_type=authorization_code",
				code,
				Uri.EscapeDataString(RedirectUri),
				ClientId,
				CodeVerifier,
				ClientSecret
				);

			return TokenRequest(tokenRequestBody, Scopes);
		}

		public Task<OAuthToken> Refresh(OAuthToken oldToken)
		{
			if (oldToken == null)
				throw new ArgumentNullException(nameof(oldToken));

			string tokenRequestBody = string.Format("refresh_token={0}&client_id={1}&client_secret={2}&grant_type=refresh_token",
				oldToken.RefreshToken,
				ClientId,
				ClientSecret
				);

			return TokenRequest(tokenRequestBody, oldToken.Scopes);
		}

		private static T Deserialize<T>(string json)
		{
			if (string.IsNullOrWhiteSpace(json))
				return default(T);

			return Deserialize<T>(Encoding.UTF8.GetBytes(json));
		}

		private static T Deserialize<T>(byte[] json)
		{
			if (json == null || json.Length == 0)
				return default(T);

			using (var ms = new MemoryStream(json))
			{
				return Deserialize<T>(ms);
			}
		}

		private static T Deserialize<T>(Stream json)
		{
			if (json == null)
				return default(T);

			var ser = CreateSerializer(typeof(T));
			return (T)ser.ReadObject(json);
		}

		private static DataContractJsonSerializer CreateSerializer(Type type)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));

			var settings = new DataContractJsonSerializerSettings
			{
				DateTimeFormat = new DateTimeFormat("yyyy-MM-dd'T'HH:mm:ss.fffK")
			};
			return new DataContractJsonSerializer(type, settings);
		}

		// https://stackoverflow.com/questions/223063/how-can-i-create-an-httplistener-class-on-a-random-port-in-c/
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
			var request = (HttpWebRequest)WebRequest.Create(TokenEndpoint);
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
				var token = Deserialize<OAuthToken>(responseStream);
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

		private static UserInfo GetUserInfo(string accessToken)
		{
			var request = (HttpWebRequest)WebRequest.Create(UserInfoEndpoint);
			request.Method = "GET";
			request.Headers.Add(string.Format("Authorization: Bearer {0}", accessToken));
			var response = request.GetResponse();
			using (var stream = response.GetResponseStream())
			{
				return Deserialize<UserInfo>(stream);
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

		[DataContract]
		private class UserInfo
		{
			[DataMember(Name = "name")]
			public string Name { get; set; }

			[DataMember(Name = "kind")]
			public string Kind { get; set; }

			[DataMember(Name = "email")]
			public string Email { get; set; }

			[DataMember(Name = "picture")]
			public string Picture { get; set; }

			[DataMember(Name = "locale")]
			public string Locale { get; set; }

			[DataMember(Name = "family_name")]
			public string FamilyName { get; set; }

			[DataMember(Name = "given_name")]
			public string GivenName { get; set; }

			[DataMember(Name = "sub")]
			public string Id { get; set; }

			[DataMember(Name = "profile")]
			public string Profile { get; set; }

			[DataMember(Name = "gender")]
			public string Gender { get; set; }
		}
	}
}