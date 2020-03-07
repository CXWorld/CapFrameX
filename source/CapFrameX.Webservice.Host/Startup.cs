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
using CapFrameX.Webservice.Persistance;
using Microsoft.EntityFrameworkCore;
using CapFrameX.Webservice.Data.Mappings;

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
				typeof(GetSessionCollectionByIdHandler).GetTypeInfo().Assembly,
				typeof(GetSessionCollectionByIdQuery).GetTypeInfo().Assembly,
				typeof(UploadSessionsCommand).GetTypeInfo().Assembly
			);
			services.AddAutoMapper(typeof(SessionCollectionProfile).Assembly);

			services.AddScoped<ISessionService, SessionService>();
			services.AddScoped<IUserClaimsProvider, UserClaimsProvider>();
			services.AddDbContext<CXContext>(options => {
				options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection"));
				options.UseLazyLoadingProxies();
			});

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

			services.AddControllers()
				.AddNewtonsoftJson(options => {
					options.SerializerSettings.TypeNameHandling = Newtonsoft.Json.TypeNameHandling.Auto;
				})
				.AddFluentValidation(opt =>
				{
					opt.RegisterValidatorsFromAssemblyContaining<GetCaptureByIdQueryValidator>();
					opt.RegisterValidatorsFromAssemblyContaining<UploadSessionsCommandValidator>();
					opt.ImplicitlyValidateChildProperties = true;
					opt.RunDefaultMvcValidationAfterFluentValidationExecutes = false;
				});
			services.AddHttpContextAccessor();
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
		{
			using (var scope = app.ApplicationServices.CreateScope())
			{
				MigrateDatabase(scope.ServiceProvider.GetRequiredService<CXContext>());
			}
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

		private void MigrateDatabase(CXContext context)
		{
			context.Database.Migrate();
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
