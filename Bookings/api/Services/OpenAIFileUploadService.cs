using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using System.Collections.Generic; // Added for List<OpenAIFileInfo>

namespace BookingsApi.Services
{
    public class OpenAIFileUploadService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _baseUrl = "https://api.openai.com/v1";

        public OpenAIFileUploadService()
        {
            _httpClient = new HttpClient();
            _apiKey = Environment.GetEnvironmentVariable("OpenAI_API_Key");
            
            if (string.IsNullOrEmpty(_apiKey))
            {
                throw new InvalidOperationException("OpenAI_API_Key environment variable is not configured");
            }
            
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        public async Task<OpenAIFileUploadResult> UploadFileAsync(string content, string filename, ILogger log)
        {
            try
            {
                log.LogInformation($"Uploading file {filename} to OpenAI...");

                // Create multipart form data
                using var formData = new MultipartFormDataContent();
                
                // Convert content to bytes
                var contentBytes = Encoding.UTF8.GetBytes(content);
                var contentStream = new MemoryStream(contentBytes);
                
                // Add the file content
                var fileContent = new StreamContent(contentStream);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                formData.Add(fileContent, "file", filename);
                
                // Add the purpose parameter
                formData.Add(new StringContent("assistants"), "purpose");

                // Make the API call
                var response = await _httpClient.PostAsync($"{_baseUrl}/files", formData);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    log.LogInformation($"OpenAI response: {responseContent}");
                    
                    var uploadResult = JsonSerializer.Deserialize<OpenAIFileResponse>(responseContent);
                    
                    log.LogInformation($"Successfully uploaded file to OpenAI. File ID: {uploadResult?.Id}");
                    
                    return new OpenAIFileUploadResult
                    {
                        Success = true,
                        FileId = uploadResult?.Id,
                        Filename = uploadResult?.Filename,
                        Purpose = uploadResult?.Purpose,
                        Bytes = uploadResult?.Bytes ?? 0
                    };
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    log.LogError($"Failed to upload file to OpenAI. Status: {response.StatusCode}, Error: {errorContent}");
                    
                    return new OpenAIFileUploadResult
                    {
                        Success = false,
                        ErrorMessage = $"HTTP {response.StatusCode}: {errorContent}"
                    };
                }
            }
            catch (Exception ex)
            {
                log.LogError($"Exception uploading file to OpenAI: {ex.Message}");
                return new OpenAIFileUploadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<bool> DeleteFileAsync(string fileId, ILogger log)
        {
            try
            {
                log.LogInformation($"Deleting file {fileId} from OpenAI...");
                
                var response = await _httpClient.DeleteAsync($"{_baseUrl}/files/{fileId}");
                
                if (response.IsSuccessStatusCode)
                {
                    log.LogInformation($"Successfully deleted file {fileId} from OpenAI");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    log.LogError($"Failed to delete file from OpenAI. Status: {response.StatusCode}, Error: {errorContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                log.LogError($"Exception deleting file from OpenAI: {ex.Message}");
                return false;
            }
        }

        public async Task<OpenAIFileListResult> ListFilesAsync(ILogger log)
        {
            try
            {
                log.LogInformation("Listing files from OpenAI...");
                
                var response = await _httpClient.GetAsync($"{_baseUrl}/files");
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var fileList = JsonSerializer.Deserialize<OpenAIFileListResponse>(responseContent);
                    
                    log.LogInformation($"Successfully retrieved {fileList?.Data?.Count ?? 0} files from OpenAI");
                    
                    return new OpenAIFileListResult
                    {
                        Success = true,
                        Files = fileList?.Data ?? new List<OpenAIFileInfo>()
                    };
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    log.LogError($"Failed to list files from OpenAI. Status: {response.StatusCode}, Error: {errorContent}");
                    
                    return new OpenAIFileListResult
                    {
                        Success = false,
                        ErrorMessage = $"HTTP {response.StatusCode}: {errorContent}"
                    };
                }
            }
            catch (Exception ex)
            {
                log.LogError($"Exception listing files from OpenAI: {ex.Message}");
                return new OpenAIFileListResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }
    }

    public class OpenAIFileUploadResult
    {
        public bool Success { get; set; }
        public string? FileId { get; set; }
        public string? Filename { get; set; }
        public string? Purpose { get; set; }
        public long Bytes { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class OpenAIFileListResult
    {
        public bool Success { get; set; }
        public List<OpenAIFileInfo> Files { get; set; } = new List<OpenAIFileInfo>();
        public string? ErrorMessage { get; set; }
    }

    public class OpenAIFileResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
        
        [JsonPropertyName("object")]
        public string? Object { get; set; }
        
        [JsonPropertyName("bytes")]
        public long Bytes { get; set; }
        
        [JsonPropertyName("created_at")]
        public long Created_at { get; set; }
        
        [JsonPropertyName("filename")]
        public string? Filename { get; set; }
        
        [JsonPropertyName("purpose")]
        public string? Purpose { get; set; }
        
        [JsonPropertyName("status")]
        public string? Status { get; set; }
        
        [JsonPropertyName("expires_at")]
        public long? Expires_at { get; set; }
        
        [JsonPropertyName("status_details")]
        public object? Status_details { get; set; }
    }

    public class OpenAIFileListResponse
    {
        [JsonPropertyName("object")]
        public string? Object { get; set; }
        
        [JsonPropertyName("data")]
        public List<OpenAIFileInfo>? Data { get; set; }
    }

    public class OpenAIFileInfo
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
        
        [JsonPropertyName("object")]
        public string? Object { get; set; }
        
        [JsonPropertyName("bytes")]
        public long Bytes { get; set; }
        
        [JsonPropertyName("created_at")]
        public long Created_at { get; set; }
        
        [JsonPropertyName("filename")]
        public string? Filename { get; set; }
        
        [JsonPropertyName("purpose")]
        public string? Purpose { get; set; }
        
        [JsonPropertyName("status")]
        public string? Status { get; set; }
        
        [JsonPropertyName("expires_at")]
        public long? Expires_at { get; set; }
        
        [JsonPropertyName("status_details")]
        public object? Status_details { get; set; }
    }
} 