
namespace WebsiteMonitorService.Services
{
	public interface IStorageService
	{
		Task<string?> GetPreviousContentAsync( string url );
		Task SaveContentAsync( string url, string content );
		Task<List<string>> GetPreviousSentencesAsync( string url );
	}
}
