using OpenAI;
using BookingsApi.Tools;
using BookingsApi.Models;
using BookingsApi.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Logging;

namespace BookingsApi.Agents
{
    /// <summary>
    /// Agent responsible for handling queries about Box League Results.
    /// Uses OpenAI file search to query the uploaded box results data.
    /// Automatically finds the file ID for summer_friendlies_all_results.json.
    /// </summary>
    public class BoxResultsAgent : IAgent
    {
        private string? _fileId;
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;
        private readonly OpenAIFileUploadService _openAIService;

        public string Name => "box_results";
        public string Description => "Handles queries about box league results, player statistics, match history, and league standings";

        public BoxResultsAgent()
        {
            _apiKey = Environment.GetEnvironmentVariable("OpenAI_API_Key");
            if (string.IsNullOrEmpty(_apiKey))
            {
                throw new InvalidOperationException("OpenAI_API_Key environment variable is not configured");
            }
            
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            _openAIService = new OpenAIFileUploadService();
        }

        public async Task<string> HandleAsync(string prompt, string? userId = null, string? sessionId = null)
        {
            try
            {
                // Get or find the file ID for summer_friendlies_all_results.json
                var fileId = await GetOrFindFileId();
                if (string.IsNullOrEmpty(fileId))
                {
                    return "I'm sorry, but I couldn't find the box results data file. Please ensure the data has been uploaded first.";
                }

                // Use OpenAI's responses API with file search
                var requestBody = new
                {
                    model = "gpt-4o-mini",
                    input = prompt,
                    tools = new[]
                    {
                        new
                        {
                            type = "file_search",
                            file_ids = new[] { fileId }
                        }
                    }
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("https://api.openai.com/v1/responses", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonSerializer.Deserialize<OpenAIResponseResult>(responseContent);
                    
                    if (apiResponse?.OutputItems != null && apiResponse.OutputItems.Count > 0)
                    {
                        var results = new List<string>();
                        
                        foreach (var item in apiResponse.OutputItems)
                        {
                            // Look for message items which contain the actual response
                            if (item.Type == "message" && item.Content != null)
                            {
                                foreach (var contentItem in item.Content)
                                {
                                    if (contentItem.Type == "text" && !string.IsNullOrEmpty(contentItem.Text))
                                    {
                                        results.Add(contentItem.Text);
                                    }
                                }
                            }
                        }
                        
                        if (results.Any())
                        {
                            return string.Join("\n\n", results);
                        }
                    }
                    
                    return "No relevant results found for your query.";
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return $"Error querying file: {response.StatusCode} - {errorContent}";
                }
            }
            catch (Exception ex)
            {
                return $"I apologize, but I encountered an error while processing your request: {ex.Message}. Please try rephrasing your question.";
            }
        }

        private async Task<string?> GetOrFindFileId()
        {
            // If we already have a file ID, use it
            if (!string.IsNullOrEmpty(_fileId))
            {
                return _fileId;
            }

            try
            {
                // Create a mock logger for the service call
                var mockLogger = new MockLogger();
                
                // Use the same logic as ListOpenAIFilesFunction to get files
                var result = await _openAIService.ListFilesAsync(mockLogger);
                
                if (result.Success && result.Files != null)
                {
                    // Find the file with filename "summer_friendlies_all_results.json"
                    var targetFile = result.Files.FirstOrDefault(f => 
                        f.Filename == "summer_friendlies_all_results.json");
                    
                    if (targetFile != null && !string.IsNullOrEmpty(targetFile.Id))
                    {
                        _fileId = targetFile.Id; // Cache it for future use
                        return _fileId;
                    }
                }
                
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Mock logger for the service call
        private class MockLogger : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => false;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
        }

        // Response models for OpenAI responses API
        public class OpenAIResponseResult
        {
            [JsonPropertyName("output_items")]
            public List<OutputItem> OutputItems { get; set; } = new List<OutputItem>();
        }

        public class OutputItem
        {
            [JsonPropertyName("type")]
            public string Type { get; set; } = string.Empty;
            
            [JsonPropertyName("role")]
            public string? Role { get; set; }
            
            [JsonPropertyName("content")]
            public List<ContentItem>? Content { get; set; }
        }

        public class ContentItem
        {
            [JsonPropertyName("type")]
            public string Type { get; set; } = string.Empty;
            
            [JsonPropertyName("text")]
            public string? Text { get; set; }
        }


    }
} 