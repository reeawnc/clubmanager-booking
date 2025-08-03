# Test script for the PromptFunction
$uri = "http://localhost:7071/api/PromptFunction"
$body = @{
    prompt = "Hello, can you tell me what time it is?"
    userId = "testuser123"
    sessionId = "testsession456"
} | ConvertTo-Json

$headers = @{
    "Content-Type" = "application/json"
}

Write-Host "Testing PromptFunction..."
Write-Host "URI: $uri"
Write-Host "Body: $body"

try {
    $response = Invoke-RestMethod -Uri $uri -Method Post -Body $body -Headers $headers
    Write-Host "Response received:"
    $response | ConvertTo-Json -Depth 10
} catch {
    Write-Host "Error occurred:"
    Write-Host $_.Exception.Message
    if ($_.Exception.Response) {
        Write-Host "Status Code: $($_.Exception.Response.StatusCode)"
    }
}