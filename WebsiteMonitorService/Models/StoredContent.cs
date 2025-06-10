
namespace WebsiteMonitorService.Models
{
	public class StoredContent
	{
		public string Url { get; set; } = string.Empty;
		public string Content { get; set; } = string.Empty;
		public DateTime LastChecked { get; set; }
		public List<string> Sentences { get; set; } = new List<string>();
	}
}
