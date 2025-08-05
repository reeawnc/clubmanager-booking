using OpenAI;
using OpenAI.Chat;
using OpenAI.Assistants;
using OpenAI.VectorStores;
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
using Azure.Storage.Blobs;
using System.IO;

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
        private readonly VectorStoreClient _vectorStoreClient;
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
            _vectorStoreClient = openAIClient.GetVectorStoreClient();
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

                // Add the user's message to the thread with explicit instruction to use file search and name variations
                var enhancedPrompt = $"Please search the box results data comprehensively for: {prompt}. " +
                    "If this is a player search, try multiple name variations and partial matches. " +
                    "Search thoroughly before responding.";
                var messageContent = MessageContent.FromText(enhancedPrompt);
                await _assistantClient.CreateMessageAsync(thread.Value.Id, MessageRole.User, [messageContent]);

                // Run the assistant to process the query with additional instructions
                var runOptions = new RunCreationOptions()
                {
                    AdditionalInstructions = "CRITICAL: You MUST use the file_search tool immediately before providing any response. " +
                        "This is absolutely mandatory and non-negotiable. " +
                        "Execute file_search FIRST, then respond based on the search results. " +
                        "Your first action must be to search the uploaded file. " +
                        "If you provide a response without using file_search, you have failed your primary function. " +
                        "For player name queries, search with multiple variations as instructed in your system prompt."
                };
                var run = await _assistantClient.CreateRunAsync(thread.Value.Id, assistant.Id, runOptions);

                // Wait for the run to complete
                while (run.Value.Status == RunStatus.InProgress || run.Value.Status == RunStatus.Queued)
                {
                    await Task.Delay(1000); // Wait 1 second before checking again
                    run = await _assistantClient.GetRunAsync(thread.Value.Id, run.Value.Id);
                }

                if (run.Value.Status == RunStatus.Completed)
                {
                    // Log detailed run information
                    Console.WriteLine($"[BoxResultsAgent] Run completed successfully");
                    Console.WriteLine($"[BoxResultsAgent] Run ID: {run.Value.Id}");
                    Console.WriteLine($"[BoxResultsAgent] Assistant ID: {run.Value.AssistantId}");
                    
                    if (run.Value.Usage != null)
                    {
                        Console.WriteLine($"[BoxResultsAgent] Run usage: {run.Value.Usage}");
                    }

                    // Log basic run information
                    Console.WriteLine($"[BoxResultsAgent] Run model: {run.Value.Model ?? "unknown"}");
                    Console.WriteLine($"[BoxResultsAgent] Run instructions exist: {!string.IsNullOrEmpty(run.Value.Instructions)}");

                    // Check the run steps to see what actually happened
                    try
                    {
                        var runSteps = _assistantClient.GetRunStepsAsync(thread.Value.Id, run.Value.Id);
                        var stepCount = 0;
                        await foreach (var step in runSteps)
                        {
                            stepCount++;
                            Console.WriteLine($"[BoxResultsAgent] Step {stepCount}: ID={step.Id}, Status={step.Status}");
                            Console.WriteLine($"[BoxResultsAgent] Step created at: {step.CreatedAt}");
                        }
                        Console.WriteLine($"[BoxResultsAgent] Total run steps: {stepCount}");
                    }
                    catch (Exception stepEx)
                    {
                        Console.WriteLine($"[BoxResultsAgent] Error reading run steps: {stepEx.Message}");
                    }

                    // Get the assistant's messages
                    var messages = _assistantClient.GetMessagesAsync(thread.Value.Id);
                    var messageCount = 0;
                    
                    // Find the latest assistant message
                    await foreach (var message in messages)
                    {
                        messageCount++;
                        Console.WriteLine($"[BoxResultsAgent] Message {messageCount}: Role={message.Role}, CreatedAt={message.CreatedAt}");
                        
                        if (message.Role == MessageRole.Assistant && message.Content?.FirstOrDefault() is var textContent && textContent != null)
                        {
                            // Try to get the text from the content
                            if (textContent.Text != null)
                            {
                                Console.WriteLine($"[BoxResultsAgent] Assistant response length: {textContent.Text.Length} chars");
                                // Add a prefix to indicate the search was performed (but may not have used tools)
                                return $"[SEARCHED DATA - TOOLS NOT VERIFIED] {textContent.Text}";
                            }
                        }
                    }
                    Console.WriteLine($"[BoxResultsAgent] Total messages in thread: {messageCount}");
                }
                else
                {
                    Console.WriteLine($"[BoxResultsAgent] Run failed with status: {run.Value.Status}");
                    if (!string.IsNullOrEmpty(run.Value.LastError?.Message))
                    {
                        Console.WriteLine($"[BoxResultsAgent] Error: {run.Value.LastError.Message}");
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
            // Always create a new assistant to ensure fresh instructions that force file search
            // This ensures the assistant will always use the file_search tool
            if (!string.IsNullOrEmpty(_assistantId))
            {
                try
                {
                    // Delete the old assistant to force creation of new one with updated instructions
                    await _assistantClient.DeleteAssistantAsync(_assistantId);
                }
                catch
                {
                    // Ignore deletion errors
                }
                _assistantId = null;
            }

            // Get the file ID for our box results
            var fileId = await GetFileId();
            if (string.IsNullOrEmpty(fileId))
            {
                return null;
            }

            // Step 1: Use VectorStoreCreationHelper (this approach should work)
            Console.WriteLine($"[BoxResultsAgent] Creating assistant with vector store for file: {fileId}");
            AssistantCreationOptions assistantOptions = new()
            {
                Name = "Box Results RAG Assistant",
                Instructions = 
                    "You are an expert assistant for box league tennis results. " +
                    "CRITICAL REQUIREMENT: You MUST use the file_search tool first before answering ANY question. This is mandatory. " +
                    "NEVER respond without first using file_search. The file_search tool is your primary source of information. " +
                    "STEP 1: Always call file_search tool with your query " +
                    "STEP 2: Only after getting file_search results, then provide your answer " +
                    "The uploaded file contains JSON data with box league results including player names, scores, dates, and match information. " +
                    "For player name searches: " +
                    "- Use file_search multiple times with name variations (full name, surname only, initials) " +
                    "- Example: For 'R Cunniffe' search: 'R Cunniffe', then 'Cunniffe', then 'Rioghan Cunniffe' " +
                    "- The data has 'player1' and 'player2' fields that contain player names " +
                    "DO NOT make up information. Only use data from file_search results. " +
                    "If file_search returns no results after trying variations, then say no data was found.",
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
                            new VectorStoreCreationHelper([fileId])
                        }
                    }
                },
            };

            var assistant = await _assistantClient.CreateAssistantAsync("gpt-4o", assistantOptions);
            _assistantId = assistant.Value.Id;
            
            Console.WriteLine($"[BoxResultsAgent] Created assistant: {assistant.Value.Id}");
            Console.WriteLine($"[BoxResultsAgent] Assistant name: {assistant.Value.Name}");
            Console.WriteLine($"[BoxResultsAgent] Assistant tools count: {assistant.Value.Tools?.Count ?? 0}");
            Console.WriteLine($"[BoxResultsAgent] File ID used: {fileId}");
            if (assistant.Value.Tools != null)
            {
                foreach (var tool in assistant.Value.Tools)
                {
                    Console.WriteLine($"[BoxResultsAgent] Tool: {tool}");
                }
            }
            
            if (assistant.Value.ToolResources?.FileSearch?.VectorStoreIds != null)
            {
                Console.WriteLine($"[BoxResultsAgent] Vector stores attached: {string.Join(", ", assistant.Value.ToolResources.FileSearch.VectorStoreIds)}");
            }
            
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
                        Console.WriteLine($"[BoxResultsAgent] Found existing file in OpenAI: {targetFile.Id}");
                        return targetFile.Id;
                    }
                }
                
                // File doesn't exist in OpenAI, try to generate and upload it
                Console.WriteLine("[BoxResultsAgent] File not found in OpenAI, attempting to generate and upload...");
                return await GenerateAndUploadBoxResultsFile(mockLogger);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BoxResultsAgent] Error in GetFileId: {ex.Message}");
                return null;
            }
        }

        private async Task<string?> GenerateAndUploadBoxResultsFile(ILogger logger)
        {
            try
            {
                Console.WriteLine("[BoxResultsAgent] Starting box results generation...");
                
                // First, try to download from Azure Blob if it exists
                var fileContent = await DownloadFromAzureBlob("summer_friendlies_all_results.json", logger);
                
                if (string.IsNullOrEmpty(fileContent))
                {
                    // File doesn't exist in blob, generate it using ProcessBoxResultsService
                    Console.WriteLine("[BoxResultsAgent] File not found in Azure Blob, generating fresh data...");
                    var processService = new ProcessBoxResultsService();
                    var result = await processService.ProcessAllSummerFriendliesLeaguesAsync();
                    
                    if (!string.IsNullOrEmpty(result.Content))
                    {
                        fileContent = result.Content;
                        Console.WriteLine($"[BoxResultsAgent] Generated content with {result.TotalMatches} matches from {result.LeaguesProcessed} leagues");
                        
                        // Upload to Azure Blob for future use
                        await UploadToAzureBlob(fileContent, result.Filename, logger);
                    }
                    else
                    {
                        Console.WriteLine("[BoxResultsAgent] Failed to generate box results content");
                        return null;
                    }
                }
                else
                {
                    Console.WriteLine("[BoxResultsAgent] Successfully downloaded existing file from Azure Blob");
                }

                // Delete any existing files with the same name from OpenAI
                var existingFiles = await _openAIService.ListFilesAsync(logger);
                if (existingFiles.Success && existingFiles.Files != null)
                {
                    var filesToDelete = existingFiles.Files
                        .Where(f => f.Filename == "summer_friendlies_all_results.json")
                        .ToList();
                    
                    foreach (var file in filesToDelete)
                    {
                        Console.WriteLine($"[BoxResultsAgent] Deleting existing OpenAI file: {file.Filename} (ID: {file.Id})");
                        await _openAIService.DeleteFileAsync(file.Id!, logger);
                    }
                }

                // Upload to OpenAI
                Console.WriteLine("[BoxResultsAgent] Uploading file to OpenAI...");
                var uploadResult = await _openAIService.UploadFileAsync(fileContent, "summer_friendlies_all_results.json", logger);
                
                if (uploadResult.Success && !string.IsNullOrEmpty(uploadResult.FileId))
                {
                    Console.WriteLine($"[BoxResultsAgent] Successfully uploaded file to OpenAI: {uploadResult.FileId}");
                    return uploadResult.FileId;
                }
                else
                {
                    Console.WriteLine($"[BoxResultsAgent] Failed to upload file to OpenAI: {uploadResult.ErrorMessage}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BoxResultsAgent] Error generating and uploading file: {ex.Message}");
                return null;
            }
        }

        private async Task<string?> DownloadFromAzureBlob(string filename, ILogger logger)
        {
            try
            {
                var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
                if (string.IsNullOrEmpty(connectionString))
                {
                    Console.WriteLine("[BoxResultsAgent] AzureWebJobsStorage connection string not configured");
                    return null;
                }

                var blobServiceClient = new BlobServiceClient(connectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient("box-results");
                var blobClient = containerClient.GetBlobClient(filename);

                if (await blobClient.ExistsAsync())
                {
                    using var stream = new MemoryStream();
                    await blobClient.DownloadToAsync(stream);
                    stream.Position = 0;
                    using var reader = new StreamReader(stream);
                    return await reader.ReadToEndAsync();
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BoxResultsAgent] Error downloading from Azure Blob: {ex.Message}");
                return null;
            }
        }

        private async Task UploadToAzureBlob(string content, string filename, ILogger logger)
        {
            try
            {
                var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
                if (string.IsNullOrEmpty(connectionString))
                {
                    Console.WriteLine("[BoxResultsAgent] AzureWebJobsStorage connection string not configured for upload");
                    return;
                }

                var blobServiceClient = new BlobServiceClient(connectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient("box-results");
                await containerClient.CreateIfNotExistsAsync();
                var blobClient = containerClient.GetBlobClient(filename);

                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
                await blobClient.UploadAsync(stream, overwrite: true);
                
                Console.WriteLine($"[BoxResultsAgent] Successfully uploaded {filename} to Azure Blob");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BoxResultsAgent] Error uploading to Azure Blob: {ex.Message}");
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