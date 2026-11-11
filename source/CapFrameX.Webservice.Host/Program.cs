using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace CapFrameX.Webservice.Host
{
	public class Program
	{
		public static void Main(string[] args)
		{
			CreateHostBuilder(args).Build().Run();
		}

		// Generic host: WebHost/IWebHostBuilder are obsolete in ASP.NET Core 10, and
		// Serilog.AspNetCore only hooks into IHostBuilder.
		public static IHostBuilder CreateHostBuilder(string[] args)
		{
			return Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
			.ConfigureAppConfiguration((context, config) =>
			{
				config
					.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
					.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true)
					.AddEnvironmentVariables();
				context.Configuration = config.Build();
			})
			.ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>())
			.UseSerilog((hostingContext, loggerConfiguration) =>
			{
				loggerConfiguration
					.ReadFrom.Configuration(hostingContext.Configuration)
					.Enrich.FromLogContext()
					.WriteTo.Console();
			});
		}
	}
}
