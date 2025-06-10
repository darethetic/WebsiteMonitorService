using Quartz;
using WebsiteMonitorService.Services;

namespace WebsiteMonitorService.Jobs
{
	public class WebsiteCheckJob : IJob
	{
		private readonly ILogger<WebsiteCheckJob> _logger;
		private readonly IWebsiteChecker _websiteChecker;
		private readonly IEmailService _emailService;
		private readonly IConfiguration _config;

		public WebsiteCheckJob(
			ILogger<WebsiteCheckJob> logger,
			IWebsiteChecker websiteChecker,
			IEmailService emailService,
			IConfiguration config )
		{
			_logger = logger;
			_websiteChecker = websiteChecker;
			_emailService = emailService;
			_config = config;
		}

		public async Task Execute( IJobExecutionContext context )
		{
			var jobStartTime = DateTime.Now;
			_logger.LogInformation( "=== Website Check Job Started at {StartTime} ===", jobStartTime );

			try
			{
				// Get configuration
				var websiteConfig = _config.GetSection( "WebsiteMonitor" );
				var websiteUrl = websiteConfig["Url"];
				var contentSelector = websiteConfig["ContentSelector"];

				if( string.IsNullOrEmpty( websiteUrl ) )
				{
					_logger.LogError( "Website URL is not configured in appsettings.json" );
					return;
				}

				_logger.LogInformation( "Checking website: {Url}", websiteUrl );
				if( !string.IsNullOrEmpty( contentSelector ) )
				{
					_logger.LogInformation( "Using content selector: {Selector}", contentSelector );
				}

				// Perform the website check
				var (hasNewText, newTextFound) = await _websiteChecker.CheckForNewTextAsync( websiteUrl, contentSelector );

				if( hasNewText )
				{
					_logger.LogInformation( "✅ NEW TEXT DETECTED! Sending notification..." );

					var subject = $"Website Change Detected - {GetDomainFromUrl( websiteUrl )}";
					var body = CreateNotificationBody( websiteUrl, newTextFound );

					await _emailService.SendNotificationAsync( subject, body, newTextFound );

					_logger.LogInformation( "📧 Notification email sent successfully" );
				}
				else
				{
					_logger.LogInformation( "ℹ️ No new text detected on the website" );

					// Log some details about what was checked
					if( !string.IsNullOrEmpty( newTextFound ) )
					{
						_logger.LogInformation( "Note: {Note}", newTextFound );
					}
				}

				var jobDuration = DateTime.Now - jobStartTime;
				_logger.LogInformation( "=== Website Check Job Completed in {Duration}ms ===",
					jobDuration.TotalMilliseconds );
			}
			catch( HttpRequestException ex )
			{
				_logger.LogError( ex, "❌ Network error while checking website. Check your internet connection or website URL." );
				await SendErrorNotification( $"Network error: {ex.Message}" );
			}
			catch( Exception ex )
			{
				_logger.LogError( ex, "❌ Unexpected error during website check" );
				await SendErrorNotification( $"Unexpected error: {ex.Message}" );
			}
		}

		private string CreateNotificationBody( string websiteUrl, string newTextFound )
		{
			var preview = newTextFound.Length > 300
				? newTextFound.Substring( 0, 300 ) + "..."
				: newTextFound;

			return $@"New content has been detected on the monitored website.

					Website: {websiteUrl}
					Detection Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

					Preview of new text:
					{preview}

					Please visit the website to see the full changes.";
		}

		private async Task SendErrorNotification( string errorMessage )
		{
			try
			{
				var websiteUrl = _config.GetSection( "WebsiteMonitor" )["Url"] ?? "Unknown";
				var subject = "Website Monitor - Error Alert";
				var body = $@"An error occurred while monitoring the website.

Website: {websiteUrl}
Error: {errorMessage}
Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

Please check the service logs for more details.";

				await _emailService.SendNotificationAsync( subject, body );
				_logger.LogInformation( "Error notification sent" );
			}
			catch( Exception ex )
			{
				_logger.LogError( ex, "Failed to send error notification" );
			}
		}

		private string GetDomainFromUrl( string url )
		{
			try
			{
				var uri = new Uri( url );
				return uri.Host;
			}
			catch
			{
				return "Website";
			}
		}
	}
}