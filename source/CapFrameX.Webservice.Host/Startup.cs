using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AutoMapper;
using CapFrameX.Webservice.Data;
using CapFrameX.Webservice.Data.Commands;
using CapFrameX.Webservice.Data.Interfaces;
using CapFrameX.Webservice.Data.Queries;
using CapFrameX.Webservice.Implementation.Handlers;
using CapFrameX.Webservice.Implementation.CaptureStorages;
using CapFrameX.Webservice.Implementation.Services;
using FluentValidation.AspNetCore;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using CapFrameX.Webservice.Data.Providers;

namespace CapFrameX.Webservice.Host
{
	public class Startup
	{
		public Startup(IConfiguration configuration)
		{
			Configuration = configuration;
		}

		public IConfiguration Configuration { get; }

		// This method gets called by the runtime. Use this method to add services to the container.
		public void ConfigureServices(IServiceCollection services)
		{
			var assembly = Assembly.GetExecutingAssembly();
			services.AddMediatR(
				typeof(GetCaptureCollectionByIdHandler).GetTypeInfo().Assembly,
				typeof(GetCaptureCollectionByIdQuery).GetTypeInfo().Assembly,
				typeof(UploadCapturesCommand).GetTypeInfo().Assembly
			);
			services.AddAutoMapper(assembly);

			services.AddScoped<ICapturesService, CapturesService>();
			services.AddScoped<ICaptureStorage>(opt =>
			{
				var storageType = Configuration.GetValue<string>("CaptureStorage:Type");
				return storageType switch
				{
					"Disk" => new CaptureDiskStorage(Configuration.GetValue<string>("CaptureStorage:Options:Directory")),
					"MongoDB" => new MongoDBStorage(Configuration.GetSection("CaptureStorage:Options").Get<MongoDbStorageConfiguration>()),
					_ => throw new Exception("No CaptureStorage configured"),
				};
			});

			services.AddScoped<IUserClaimsProvider, UserClaimsProvider>();

			services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
				.AddJwtBearer(options =>
				{
					options.Authority = @"https://capframex.com/auth/realms/CapFrameX";
					options.Audience = "account";
					options.TokenValidationParameters = new TokenValidationParameters
					{
						ValidateIssuerSigningKey = true,
						ValidateIssuer = true,
						ValidateAudience = false,
						ValidateLifetime = true
					};
				});

			services.AddControllers()
				.AddFluentValidation(opt =>
				{
					opt.RegisterValidatorsFromAssemblyContaining<GetCaptureByIdQueryValidator>();
					opt.RegisterValidatorsFromAssemblyContaining<UploadCaptureCommandValidator>();
					opt.ImplicitlyValidateChildProperties = true;
					opt.RunDefaultMvcValidationAfterFluentValidationExecutes = false;
				});
			services.AddHttpContextAccessor();
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
		{
			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}

			app.UseSerilogRequestLogging();
			app.UseForwardedHeaders(GetHeaderOptions());
			app.UseRouting();

			app.UseCors(x => x
				.AllowAnyOrigin()
				.AllowAnyMethod()
				.AllowAnyHeader());

			app.UseAuthentication();
			app.UseAuthorization();

			app.UseEndpoints(endpoints =>
			{
				endpoints.MapControllers();
			});
		}

		private ForwardedHeadersOptions GetHeaderOptions()
		{
			var fordwardedHeaderOptions = new ForwardedHeadersOptions
			{
				ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
			};
			fordwardedHeaderOptions.KnownNetworks.Clear();
			fordwardedHeaderOptions.KnownProxies.Clear();
			return fordwardedHeaderOptions;
		}
	}
}
