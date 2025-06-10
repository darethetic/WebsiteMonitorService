using HtmlAgilityPack;
using System.Net;

namespace WebsiteMonitorService.Services
{
	public class WebsiteChecker : IWebsiteChecker
	{
		private readonly HttpClient _httpClient;
		private readonly IStorageService _storage;
		private readonly ILogger<WebsiteChecker> _logger;

		public WebsiteChecker( HttpClient httpClient, IStorageService storage, ILogger<WebsiteChecker> logger )
		{
			_httpClient = httpClient;
			_storage = storage;
			_logger = logger;

			// Configure HttpClient for better compatibility
			_httpClient.DefaultRequestHeaders.Add( "User-Agent",
				"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36" );
		}

		public async Task<(bool HasNewText, string NewTextFound)> CheckForNewTextAsync( string url, string? contentSelector = null )
		{
			try
			{
				_logger.LogInformation( "Starting website check for: {Url}", url );

				// Fetch current content
				var html = await FetchWebsiteContentAsync( url );
				if( string.IsNullOrEmpty( html ) )
				{
					_logger.LogWarning( "Failed to fetch content from {Url}", url );
					return (false, string.Empty);
				}

				// Extract clean text content
				var currentText = ExtractCleanText( html, contentSelector );

				if( string.IsNullOrWhiteSpace( currentText ) )
				{
					_logger.LogWarning( "No text content extracted from {Url}", url );
					return (false, string.Empty);
				}

				_logger.LogInformation( "Extracted {Length} characters of text content", currentText.Length );

				// Get previous sentences for comparison
				var previousSentences = await _storage.GetPreviousSentencesAsync( url );

				// Split current content into sentences
				var currentSentences = SplitIntoSentences( currentText );

				// Check if this is the first time checking
				if( !previousSentences.Any() )
				{
					_logger.LogInformation( "First time checking {Url} - saving baseline with {Count} sentences",
						url, currentSentences.Count );
					await _storage.SaveContentAsync( url, currentText );
					return (false, "Baseline saved - no notification sent for first check");
				}

				// Find new sentences (present in current but not in previous)
				var newSentences = currentSentences
					.Except( previousSentences, StringComparer.OrdinalIgnoreCase )
					.ToList();

				if( newSentences.Any() )
				{
					var newTextFound = string.Join( ". ", newSentences );

					_logger.LogInformation( "Found {Count} new sentences on {Url}", newSentences.Count, url );
					_logger.LogInformation( "New text preview: {Preview}",
						newTextFound.Length > 200 ? newTextFound.Substring( 0, 200 ) + "..." : newTextFound );

					// Save updated content
					await _storage.SaveContentAsync( url, currentText );

					return (true, newTextFound);
				}

				_logger.LogInformation( "No new text found on {Url}", url );
				return (false, string.Empty);
			}
			catch( Exception ex )
			{
				_logger.LogError( ex, "Error checking website {Url}", url );
				throw;
			}
		}

		private async Task<string> FetchWebsiteContentAsync( string url )
		{
			try
			{
				var response = await _httpClient.GetAsync( url );

				if( response.StatusCode == HttpStatusCode.OK )
				{
					return await response.Content.ReadAsStringAsync();
				}

				_logger.LogWarning( "HTTP {StatusCode} when fetching {Url}", response.StatusCode, url );
				return string.Empty;
			}
			catch( HttpRequestException ex )
			{
				_logger.LogError( ex, "HTTP error fetching {Url}", url );
				throw;
			}
		}

		private string ExtractCleanText( string html, string? contentSelector )
		{
			try
			{
				var doc = new HtmlDocument();
				doc.LoadHtml( html );

				HtmlNode? targetNode = null;

				// Use selector if provided
				if( !string.IsNullOrEmpty( contentSelector ) )
				{
					if( contentSelector.StartsWith( "#" ) )
					{
						// ID selector
						var id = contentSelector.Substring( 1 );
						targetNode = doc.GetElementbyId( id );
					}
					else if( contentSelector.StartsWith( "." ) )
					{
						// Class selector
						var className = contentSelector.Substring( 1 );
						targetNode = doc.DocumentNode
							.Descendants()
							.FirstOrDefault( n => n.GetClasses().Contains( className ) );
					}
					else
					{
						// Tag selector
						targetNode = doc.DocumentNode
							.Descendants( contentSelector )
							.FirstOrDefault();
					}

					if( targetNode == null )
					{
						_logger.LogWarning( "Could not find element with selector: {Selector}", contentSelector );
						targetNode = doc.DocumentNode;
					}
				}
				else
				{
					targetNode = doc.DocumentNode;
				}

				// Remove script and style elements
				var scripts = targetNode.Descendants( "script" ).ToArray();
				var styles = targetNode.Descendants( "style" ).ToArray();

				foreach( var script in scripts ) script.Remove();
				foreach( var style in styles ) style.Remove();

				// Extract text content
				var textContent = targetNode.InnerText;

				// Clean up the text
				textContent = WebUtility.HtmlDecode( textContent );
				textContent = System.Text.RegularExpressions.Regex.Replace( textContent, @"\s+", " " );
				textContent = textContent.Trim();

				return textContent;
			}
			catch( Exception ex )
			{
				_logger.LogError( ex, "Error extracting text content" );
				return html; // Fallback to raw HTML
			}
		}

		private List<string> SplitIntoSentences( string text )
		{
			if( string.IsNullOrWhiteSpace( text ) )
				return new List<string>();

			// Split by sentence endings and clean up
			var sentences = text
				.Split( new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries )
				.Select( s => s.Trim() )
				.Where( s => s.Length > 15 ) // Ignore very short fragments
				.Where( s => !string.IsNullOrWhiteSpace( s ) )
				.Select( s => s.ToLowerInvariant() ) // Normalize for comparison
				.ToList();

			return sentences;
		}
	}
}