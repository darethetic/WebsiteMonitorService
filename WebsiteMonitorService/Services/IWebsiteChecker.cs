
namespace WebsiteMonitorService.Services
{
	public interface IWebsiteChecker
	{
		Task<(bool HasNewText, string NewTextFound)> CheckForNewTextAsync( string url, string? contentSelector = null );
	}
}
