
namespace WebsiteMonitorService.Services
{
	public interface IEmailService
	{
		Task SendNotificationAsync( string subject, string body, string? newText = null );
	}
}
