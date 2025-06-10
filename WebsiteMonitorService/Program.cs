using WebsiteMonitorService.Extensions;

namespace WebsiteMonitorService
{
	public class Program
	{
		public static async Task Main( string[] args )
		{
			// Create host builder
			var host = Host.CreateDefaultBuilder( args )
				.UseWindowsService( options =>
				{
					options.ServiceName = "Website Monitor Service";
				} )
				.ConfigureAppConfiguration( ( context, config ) =>
				{
					// Add configuration sources
					config.AddJsonFile( "appsettings.json", optional: false, reloadOnChange: true );
					config.AddJsonFile( $"appsettings.{context.HostingEnvironment.EnvironmentName}.json",
						optional: true, reloadOnChange: true );
					config.AddEnvironmentVariables();

					// Add user secrets in development
					if( context.HostingEnvironment.IsDevelopment() )
					{
						config.AddUserSecrets<Program>();
					}
				} )
				.ConfigureServices( ( context, services ) =>
				{
					// Add our custom services
					services.AddWebsiteMonitorServices( context.Configuration );

					// Add Quartz scheduling
					services.AddQuartzJobs( context.Configuration );
				} )
				.ConfigureLogging( ( context, logging ) =>
				{
					logging.ClearProviders();
					logging.AddConsole();

					// Configure log levels
					logging.AddFilter( "Microsoft", LogLevel.Warning );
					logging.AddFilter( "System", LogLevel.Warning );
					logging.AddFilter( "Quartz", LogLevel.Information );
					logging.AddFilter( "WebsiteMonitorService", LogLevel.Information );

					// Add Windows Event Log when running as service
					if( OperatingSystem.IsWindows() )
					{
						logging.AddEventLog( settings =>
						{
							settings.SourceName = "Website Monitor Service";
						} );
					}
				} )
				.Build();

			// Log startup information
			var logger = host.Services.GetRequiredService<ILogger<Program>>();
			var config = host.Services.GetRequiredService<IConfiguration>();

			logger.LogInformation( "=== Website Monitor Service Starting ===" );
			logger.LogInformation( "Environment: {Environment}", host.Services.GetRequiredService<IHostEnvironment>().EnvironmentName );

			// Log configuration (without sensitive data)
			var websiteUrl = config.GetSection( "WebsiteMonitor" )["Url"];
			var cronSchedule = config.GetSection( "WebsiteMonitor" )["CheckIntervalCron"] ?? "0 0 9 ? * MON";
			var smtpServer = config.GetSection( "Email" )["SmtpServer"];

			logger.LogInformation( "Monitoring URL: {Url}", websiteUrl ?? "NOT CONFIGURED" );
			logger.LogInformation( "Check Schedule: {Schedule}", cronSchedule );
			logger.LogInformation( "SMTP Server: {SmtpServer}", smtpServer ?? "NOT CONFIGURED" );

			// Validate configuration
			if( string.IsNullOrEmpty( websiteUrl ) )
			{
				logger.LogError( "Website URL is not configured! Please update appsettings.json" );
			}

			if( string.IsNullOrEmpty( smtpServer ) )
			{
				logger.LogError( "Email configuration is missing! Please update appsettings.json" );
			}

			try
			{
				// Run the service
				logger.LogInformation( "Service started successfully" );
				await host.RunAsync();
			}
			catch( Exception ex )
			{
				logger.LogCritical( ex, "Service terminated unexpectedly" );
				throw;
			}
			finally
			{
				logger.LogInformation( "Website Monitor Service stopped" );
			}
		}
	}
}