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
using CapFrameX.Webservice.Implementation.CaptureStorageProviders;
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
			services.AddScoped<ICaptureStorage>(opt => new CaptureDiskStorage(@"C:\CXService"));

			services.AddControllers()
				.AddFluentValidation(opt =>
				{
					opt.RegisterValidatorsFromAssemblyContaining<GetCaptureByIdQueryValidator>();
					opt.RegisterValidatorsFromAssemblyContaining<UploadCaptureCommandValidator>();
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
			}

			app.UseSerilogRequestLogging();
			app.UseForwardedHeaders(GetHeaderOptions());
			app.UseRouting();

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
