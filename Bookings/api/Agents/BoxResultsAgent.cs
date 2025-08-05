using OpenAI;
using OpenAI.Chat;
using OpenAI.Assistants;
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
        private readonly string _apiKey;
#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates
        private readonly AssistantClient _assistantClient;
#pragma warning restore OPENAI001
        private readonly OpenAIFileUploadService _openAIService;
        private string? _assistantId;

        public string Name => "box_results";
        public string Description => "Handles queries about box league results, player statistics, match history, and league standings";

        public BoxResultsAgent()
        {
            _apiKey = Environment.GetEnvironmentVariable("OpenAI_API_Key");
            if (string.IsNullOrEmpty(_apiKey))
            {
                throw new InvalidOperationException("OpenAI_API_Key environment variable is not configured");
            }
            
            var openAIClient = new OpenAIClient(_apiKey);
#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates
            _assistantClient = openAIClient.GetAssistantClient();
#pragma warning restore OPENAI001
            _openAIService = new OpenAIFileUploadService();
        }

        public async Task<string> HandleAsync(string prompt, string? userId = null, string? sessionId = null)
        {
            try
            {
#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates
                // Get or create the assistant with file search capabilities
                var assistant = await GetOrCreateAssistant();
                if (assistant == null)
                {
                    return "I'm sorry, but I couldn't set up the box results assistant. Please ensure the data has been uploaded first.";
                }

                // Create a thread for this conversation
                var thread = await _assistantClient.CreateThreadAsync();

                // Add the user's message to the thread
                var messageContent = MessageContent.FromText(prompt);
                await _assistantClient.CreateMessageAsync(thread.Value.Id, MessageRole.User, [messageContent]);

                // Run the assistant to process the query
                var run = await _assistantClient.CreateRunAsync(thread.Value.Id, assistant.Id);

                // Wait for the run to complete
                while (run.Value.Status == RunStatus.InProgress || run.Value.Status == RunStatus.Queued)
                {
                    await Task.Delay(1000); // Wait 1 second before checking again
                    run = await _assistantClient.GetRunAsync(thread.Value.Id, run.Value.Id);
                }

                if (run.Value.Status == RunStatus.Completed)
                {
                    // Get the assistant's messages
                    var messages = _assistantClient.GetMessagesAsync(thread.Value.Id);
                    
                    // Find the latest assistant message
                    await foreach (var message in messages)
                    {
                        if (message.Role == MessageRole.Assistant && message.Content?.FirstOrDefault() is var textContent && textContent != null)
                        {
                            // Try to get the text from the content
                            if (textContent.Text != null)
                            {
                                return textContent.Text;
                            }
                        }
                    }
                }
#pragma warning restore OPENAI001

                return "I apologize, but I couldn't generate a response for your query. Please try rephrasing your question.";
            }
            catch (Exception ex)
            {
                return $"I apologize, but I encountered an error while processing your request: {ex.Message}. Please try rephrasing your question.";
            }
        }

#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates
        private async Task<Assistant?> GetOrCreateAssistant()
#pragma warning restore OPENAI001
        {
#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates
            // If we already have an assistant, return it
            if (!string.IsNullOrEmpty(_assistantId))
            {
                try
                {
                    var existingAssistant = await _assistantClient.GetAssistantAsync(_assistantId);
                    return existingAssistant.Value;
                }
                catch
                {
                    // Assistant might have been deleted, create a new one
                    _assistantId = null;
                }
            }

            // Get the file ID for our box results
            var fileId = await GetFileId();
            if (string.IsNullOrEmpty(fileId))
            {
                return null;
            }

            // Create assistant options following the official documentation pattern
            AssistantCreationOptions assistantOptions = new()
            {
                Name = "Box Results RAG Assistant",
                Instructions = 
                    "You are an expert assistant for box league tennis results. " +
                    "Use the uploaded box results data to answer questions about matches, players, statistics, and league standings. " +
                    "The data contains JSON objects with information about boxes, players, scores, dates, and match results. " +
                    "Provide helpful, accurate answers based on this data.",
                Tools =
                {
                    new FileSearchToolDefinition(),
                },
                ToolResources = new()
                {
                    FileSearch = new()
                    {
                        NewVectorStores =
                        {
                            new VectorStoreCreationHelper([fileId]),
                        }
                    }
                },
            };

            var assistant = await _assistantClient.CreateAssistantAsync("gpt-4o-mini", assistantOptions);
            _assistantId = assistant.Value.Id;
            return assistant.Value;
#pragma warning restore OPENAI001
        }

        private async Task<string?> GetFileId()
        {
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
                        return targetFile.Id;
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




    }
} 