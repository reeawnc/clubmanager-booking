using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace BookingsApi.Services
{
    /// <summary>
    /// Service for persisting OpenAI Assistant IDs to Azure Blob Storage.
    /// This allows reusing assistants across function invocations to improve performance.
    /// </summary>
    public class AssistantPersistenceService
    {
        private readonly ILogger _logger;
        private const string CONTAINER_NAME = "assistant-config";

        public AssistantPersistenceService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Saves an assistant ID to Azure Blob Storage.
        /// </summary>
        /// <param name="assistantType">The type/name of the assistant (e.g., "box-results")</param>
        /// <param name="assistantId">The OpenAI assistant ID</param>
        /// <returns>True if saved successfully, false otherwise</returns>
        public async Task<bool> SaveAssistantIdAsync(string assistantType, string assistantId)
        {
            if (string.IsNullOrEmpty(assistantType))
            {
                throw new ArgumentException("Assistant type is required", nameof(assistantType));
            }
            if (string.IsNullOrEmpty(assistantId))
            {
                throw new ArgumentException("Assistant ID is required", nameof(assistantId));
            }

            try
            {
                var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
                if (string.IsNullOrEmpty(connectionString))
                {
                    _logger.LogWarning("AzureWebJobsStorage connection string not configured");
                    return false;
                }

                var blobServiceClient = new BlobServiceClient(connectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(CONTAINER_NAME);
                await containerClient.CreateIfNotExistsAsync();

                var filename = $"{assistantType}-assistant-id.txt";
                var blobClient = containerClient.GetBlobClient(filename);

                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(assistantId));
                await blobClient.UploadAsync(stream, overwrite: true);
                
                _logger.LogInformation($"Successfully saved assistant ID for {assistantType}: {assistantId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving assistant ID for {assistantType}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads an assistant ID from Azure Blob Storage.
        /// </summary>
        /// <param name="assistantType">The type/name of the assistant (e.g., "box-results")</param>
        /// <returns>The assistant ID if found, null otherwise</returns>
        public async Task<string?> LoadAssistantIdAsync(string assistantType)
        {
            if (string.IsNullOrEmpty(assistantType))
            {
                throw new ArgumentException("Assistant type is required", nameof(assistantType));
            }

            try
            {
                var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
                if (string.IsNullOrEmpty(connectionString))
                {
                    _logger.LogWarning("AzureWebJobsStorage connection string not configured");
                    return null;
                }

                var blobServiceClient = new BlobServiceClient(connectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(CONTAINER_NAME);
                
                var filename = $"{assistantType}-assistant-id.txt";
                var blobClient = containerClient.GetBlobClient(filename);

                if (await blobClient.ExistsAsync())
                {
                    using var stream = new MemoryStream();
                    await blobClient.DownloadToAsync(stream);
                    stream.Position = 0;
                    using var reader = new StreamReader(stream);
                    var assistantId = await reader.ReadToEndAsync();
                    
                    if (!string.IsNullOrWhiteSpace(assistantId))
                    {
                        _logger.LogInformation($"Successfully loaded assistant ID for {assistantType}: {assistantId}");
                        return assistantId.Trim();
                    }
                }
                
                _logger.LogInformation($"No assistant ID found for {assistantType}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading assistant ID for {assistantType}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Deletes a saved assistant ID from Azure Blob Storage.
        /// Useful when an assistant becomes invalid and needs to be recreated.
        /// </summary>
        /// <param name="assistantType">The type/name of the assistant (e.g., "box-results")</param>
        /// <returns>True if deleted successfully, false otherwise</returns>
        public async Task<bool> DeleteAssistantIdAsync(string assistantType)
        {
            if (string.IsNullOrEmpty(assistantType))
            {
                throw new ArgumentException("Assistant type is required", nameof(assistantType));
            }

            try
            {
                var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
                if (string.IsNullOrEmpty(connectionString))
                {
                    _logger.LogWarning("AzureWebJobsStorage connection string not configured");
                    return false;
                }

                var blobServiceClient = new BlobServiceClient(connectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(CONTAINER_NAME);
                
                var filename = $"{assistantType}-assistant-id.txt";
                var blobClient = containerClient.GetBlobClient(filename);

                if (await blobClient.ExistsAsync())
                {
                    await blobClient.DeleteAsync();
                    _logger.LogInformation($"Successfully deleted assistant ID for {assistantType}");
                    return true;
                }
                
                _logger.LogInformation($"No assistant ID found to delete for {assistantType}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting assistant ID for {assistantType}: {ex.Message}");
                return false;
            }
        }
    }
}