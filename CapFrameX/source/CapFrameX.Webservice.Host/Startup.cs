using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AutoMapper;
using CapFrameX.Webservice.Data.Commands;
using CapFrameX.Webservice.Data.Interfaces;
using CapFrameX.Webservice.Data.Queries;
using CapFrameX.Webservice.Implementation.Handlers;
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

using CapFrameX.Webservice.Data.Mappings;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Diagnostics;
using System.Net;
using System.Net.Mime;
using CapFrameX.Webservice.Host.Attributes;
using Squidex.ClientLibrary;
using CapFrameX.Webservice.Data.Options;

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

			// Register Libraries
			services.AddMediatR(
				typeof(GetSessionCollectionByIdHandler).GetTypeInfo().Assembly, // Register Handlers
				typeof(GetSessionCollectionByIdQuery).GetTypeInfo().Assembly, // Register Queries
				typeof(UploadSessionsCommand).GetTypeInfo().Assembly // Register Commands
			);
			services.AddAutoMapper(typeof(SessionCollectionProfile).Assembly);

			services.Configure<SmtpOptions>(Configuration.GetSection("SmtpOptions"));

			// Setup and Register Squidex
			services.Configure<SquidexOptions>(Configuration.GetSection("SquidexOptions"));
			services.AddSingleton<SquidexService>();

			// Register Services
			services.AddSingleton<ISessionService>(x => x.GetRequiredService<SquidexService>());
			services.AddSingleton<IProcessListService>(x => x.GetRequiredService<SquidexService>());
			services.AddSingleton<ICrashlogReportingService>(x => x.GetRequiredService<SquidexService>());

			// Register Providers
			services.AddScoped<IUserClaimsProvider, UserClaimsProvider>();

			// Register ServiceFilters
			services.AddScoped<UserAgentFilter>();

			// Register Middlewares
			services.AddHttpContextAccessor();
			services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
				.AddJwtBearer(options =>
				{
					options.Authority = @"https://capframex.com/auth/realms/CapFrameX";
					options.Audience = "account";
					options.TokenValidationParameters = new TokenValidationParameters
					{
						ValidateIssuerSigningKey = true,
						ValidateIssuer = true,
						ValidateAudience = true,
						ValidateLifetime = true
					};
				});

			// Register Controllers
			services.AddControllers()
				.AddNewtonsoftJson()
				.AddFluentValidation(opt =>
				{
					opt.RegisterValidatorsFromAssemblyContaining<GetCaptureByIdQueryValidator>();
					opt.RegisterValidatorsFromAssemblyContaining<UploadSessionsCommandValidator>();
					opt.ImplicitlyValidateChildProperties = true;
					opt.RunDefaultMvcValidationAfterFluentValidationExecutes = false;
				});
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
		{
			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			} else
			{
				app.UseExceptionHandler(errorApp =>
				{
					errorApp.Run(async context =>
					{
						context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
						context.Response.ContentType = MediaTypeNames.Application.Json;
						var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();

						var error = new {
							context.Response.StatusCode,
							exceptionHandlerPathFeature.Error.Message
						};

						await context.Response.WriteAsync(JsonConvert.SerializeObject(error));
					});
				});
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
