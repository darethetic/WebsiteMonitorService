// Extensions/ServiceCollectionExtensions.cs
using Quartz;
using WebsiteMonitorService.Jobs;
using WebsiteMonitorService.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WebsiteMonitorService.Extensions
{
	public static class ServiceCollectionExtensions
	{
		public static IServiceCollection AddWebsiteMonitorServices( this IServiceCollection services, IConfiguration configuration )
		{
			// Add HTTP client factory first
			services.AddHttpClient();

			// Add HTTP client for WebsiteChecker
			services.AddHttpClient<WebsiteChecker>( client =>
			{
				client.Timeout = TimeSpan.FromSeconds( 30 );
			} );

			// Register services
			services.AddSingleton<IStorageService, StorageService>();
			services.AddScoped<IWebsiteChecker, WebsiteChecker>();
			services.AddScoped<IEmailService, EmailService>();

			return services;
		}

		public static IServiceCollection AddQuartzJobs( this IServiceCollection services, IConfiguration configuration )
		{
			services.AddQuartz( configure =>
			{
				// Use simple type loader and in-memory store
				configure.UseSimpleTypeLoader();
				configure.UseInMemoryStore();
				configure.UseDefaultThreadPool( tp =>
				{
					tp.MaxConcurrency = 10;
				} );

				// Create job key
				var jobKey = new JobKey( "WebsiteCheckJob" );

				// Add the job
				configure.AddJob<WebsiteCheckJob>( opts => opts.WithIdentity( jobKey ) );

				// Get cron schedule from configuration (default to Monday at 9 AM)
				var cronSchedule = configuration.GetSection( "WebsiteMonitor" )["CheckIntervalCron"]
					?? "0 0 9 ? * MON"; // Default: Every Monday at 9 AM

				// Add the trigger
				configure.AddTrigger( opts => opts
					.ForJob( jobKey )
					.WithIdentity( "WebsiteCheckTrigger" )
					.WithCronSchedule( cronSchedule )
					.WithDescription( $"Triggers website check with schedule: {cronSchedule}" )
				);
			} );

			// Add Quartz hosted service
			services.AddQuartzHostedService( q => q.WaitForJobsToComplete = true );

			return services;
		}


	}
}