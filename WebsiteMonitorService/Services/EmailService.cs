using System.Net;
using System.Net.Mail;

namespace WebsiteMonitorService.Services
{
	public class EmailService : IEmailService
	{
		private readonly IConfiguration _config;
		private readonly ILogger<EmailService> _logger;

		public EmailService( IConfiguration config, ILogger<EmailService> logger )
		{
			_config = config;
			_logger = logger;
		}

		public async Task SendNotificationAsync( string subject, string body, string? newText = null )
		{
			try
			{
				var emailConfig = _config.GetSection( "Email" );

				// Validate configuration
				if( !ValidateEmailConfiguration( emailConfig ) )
				{
					_logger.LogError( "Email configuration is incomplete" );
					return;
				}

				var smtpServer = emailConfig["SmtpServer"]!;
				var port = int.Parse( emailConfig["Port"]! );
				var enableSsl = bool.Parse( emailConfig["EnableSsl"]! );
				var username = emailConfig["Username"]!;
				var password = emailConfig["Password"]!;
				var fromEmail = emailConfig["FromEmail"]!;
				var fromName = emailConfig["FromName"]!;
				var toEmails = emailConfig["ToEmail"]!;

				using var client = new SmtpClient( smtpServer, port )
				{
					EnableSsl = enableSsl,
					Credentials = new NetworkCredential( username, password ),
					Timeout = 30000 // 30 seconds timeout
				};

				var emailBody = CreateEmailBody( body, newText );

				var message = new MailMessage
				{
					From = new MailAddress( fromEmail, fromName ),
					Subject = subject,
					Body = emailBody,
					IsBodyHtml = false,
					Priority = MailPriority.Normal
				};

				// Handle multiple recipients
				var recipients = toEmails.Split( ',', StringSplitOptions.RemoveEmptyEntries );
				foreach( var email in recipients )
				{
					var trimmedEmail = email.Trim();
					if( IsValidEmail( trimmedEmail ) )
					{
						message.To.Add( trimmedEmail );
						_logger.LogInformation( "Added recipient: {Email}", trimmedEmail );
					}
					else
					{
						_logger.LogWarning( "Invalid email address skipped: {Email}", trimmedEmail );
					}
				}

				if( message.To.Count == 0 )
				{
					_logger.LogError( "No valid email recipients found" );
					return;
				}

				_logger.LogInformation( "Sending email notification to {Count} recipients", message.To.Count );
				await client.SendMailAsync( message );
				_logger.LogInformation( "Email notification sent successfully to all recipients" );
			}
			catch( SmtpException ex )
			{
				_logger.LogError( ex, "SMTP error while sending email: {Message}", ex.Message );
				throw;
			}
			catch( Exception ex )
			{
				_logger.LogError( ex, "Unexpected error while sending email" );
				throw;
			}
		}

		private bool ValidateEmailConfiguration( IConfigurationSection emailConfig )
		{
			var requiredFields = new[] { "SmtpServer", "Port", "EnableSsl", "Username", "Password", "FromEmail", "ToEmail" };

			foreach( var field in requiredFields )
			{
				if( string.IsNullOrEmpty( emailConfig[field] ) )
				{
					_logger.LogError( "Missing email configuration: {Field}", field );
					return false;
				}
			}

			// Validate port is a number
			if( !int.TryParse( emailConfig["Port"], out _ ) )
			{
				_logger.LogError( "Invalid port number in email configuration" );
				return false;
			}

			// Validate EnableSsl is a boolean
			if( !bool.TryParse( emailConfig["EnableSsl"], out _ ) )
			{
				_logger.LogError( "Invalid EnableSsl value in email configuration" );
				return false;
			}

			return true;
		}

		private string CreateEmailBody( string mainBody, string? newText )
		{
			var emailBody = $@"Website Monitor Alert

			{mainBody}

			Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

			";

			if( !string.IsNullOrEmpty( newText ) )
			{
				emailBody += $@"New Text Detected:
				----------------------------------------
				{newText}
				----------------------------------------

				";
							}

				emailBody += @"
				This is an automated message from Website Monitor Service.
				";

			return emailBody;
		}

		private bool IsValidEmail( string email )
		{
			try
			{
				var addr = new System.Net.Mail.MailAddress( email );
				return addr.Address == email;
			}
			catch
			{
				return false;
			}
		}
	}
}