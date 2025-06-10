using System.Text.Json;
using WebsiteMonitorService.Models;

namespace WebsiteMonitorService.Services
{
	public class StorageService : IStorageService
	{
		private readonly string _dataDirectory;
		private readonly ILogger<StorageService> _logger;

		public StorageService( ILogger<StorageService> logger )
		{
			_logger = logger;
			_dataDirectory = Path.Combine(
				Environment.GetFolderPath( Environment.SpecialFolder.ApplicationData ),
				"WebsiteMonitorService"
			);

			// Create directory if it doesn't exist
			Directory.CreateDirectory( _dataDirectory );
			_logger.LogInformation( "Storage directory: {Directory}", _dataDirectory );
		}

		public async Task<string?> GetPreviousContentAsync( string url )
		{
			try
			{
				var filePath = GetFilePath( url );

				if( !File.Exists( filePath ) )
				{
					_logger.LogInformation( "No previous content found for {Url}", url );
					return null;
				}

				var json = await File.ReadAllTextAsync( filePath );
				var storedContent = JsonSerializer.Deserialize<StoredContent>( json );

				if( storedContent != null )
				{
					_logger.LogInformation( "Retrieved previous content for {Url}, last checked: {LastChecked}",
						url, storedContent.LastChecked );
					return storedContent.Content;
				}

				return null;
			}
			catch( Exception ex )
			{
				_logger.LogError( ex, "Error reading previous content for {Url}", url );
				return null;
			}
		}

		public async Task<List<string>> GetPreviousSentencesAsync( string url )
		{
			try
			{
				var filePath = GetFilePath( url );

				if( !File.Exists( filePath ) )
				{
					return new List<string>();
				}

				var json = await File.ReadAllTextAsync( filePath );
				var storedContent = JsonSerializer.Deserialize<StoredContent>( json );

				return storedContent?.Sentences ?? new List<string>();
			}
			catch( Exception ex )
			{
				_logger.LogError( ex, "Error reading previous sentences for {Url}", url );
				return new List<string>();
			}
		}

		public async Task SaveContentAsync( string url, string content )
		{
			try
			{
				var filePath = GetFilePath( url );

				// Split content into sentences for comparison
				var sentences = SplitIntoSentences( content );

				var storedContent = new StoredContent
				{
					Url = url,
					Content = content,
					LastChecked = DateTime.UtcNow,
					Sentences = sentences
				};

				var options = new JsonSerializerOptions
				{
					WriteIndented = true,
					PropertyNamingPolicy = JsonNamingPolicy.CamelCase
				};

				var json = JsonSerializer.Serialize( storedContent, options );
				await File.WriteAllTextAsync( filePath, json );

				_logger.LogInformation( "Saved content for {Url} with {SentenceCount} sentences",
					url, sentences.Count );
			}
			catch( Exception ex )
			{
				_logger.LogError( ex, "Error saving content for {Url}", url );
				throw;
			}
		}

		private string GetFilePath( string url )
		{
			// Create a safe filename from URL
			var uri = new Uri( url );
			var safeName = $"{uri.Host}_{uri.PathAndQuery}"
				.Replace( "/", "_" )
				.Replace( "?", "_" )
				.Replace( "&", "_" )
				.Replace( ":", "_" )
				.Replace( "#", "_" );

			return Path.Combine( _dataDirectory, $"{safeName}.json" );
		}

		private List<string> SplitIntoSentences( string text )
		{
			if( string.IsNullOrWhiteSpace( text ) )
				return new List<string>();

			// Split by sentence endings and clean up
			var sentences = text
				.Split( new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries )
				.Select( s => s.Trim() )
				.Where( s => s.Length > 15 )
				.Where( s => !string.IsNullOrWhiteSpace( s ) )
				.Select( s => s.ToLowerInvariant() )
				.ToList();

			return sentences;
		}
	}
}