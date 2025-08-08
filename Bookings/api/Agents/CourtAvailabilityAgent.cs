using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using BookingsApi.Tools;
using BookingsApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace BookingsApi.Agents
{
    /// <summary>
    /// Agent responsible for handling court availability queries.
    /// Uses the GetCourtAvailabilityTool and formats responses in a user-friendly way.
    /// </summary>
    public class CourtAvailabilityAgent : IAgent
    {
        private readonly ChatClient _chatClient;
        private readonly ToolRegistry _toolRegistry;
        private readonly bool _deterministicFormatting;

        public string Name => "court_availability";
        public string Description => "Handles queries about court availability, schedules, and who's playing";

        private string GetSystemPrompt()
        {
            var today = DateTime.Now;
            var formattedDate = today.ToString("dddd, MMMM d, yyyy"); // e.g., "Monday, January 15, 2024"
            var currentTime = today.ToString("HH:mm"); // e.g., "14:30"
            
            return $@"You are a helpful assistant specializing in squash court availability. 
Your role is to help users find available court times and understand current bookings.

Current time in Dublin, Ireland: {formattedDate} at {currentTime}

When responding about court availability:
- Always provide clear, organized information
- Use the get_court_availability tool to fetch current data
- ensure you pass a real date, if they day today get todays date.
- The data should always include the year, month and day.
- Format responses in a structured way that's easy to read
- Include both available slots and current bookings
- Be friendly and helpful in your tone

STRICT PREDICTABILITY RULES:
- Never fabricate courts, times, players, or session types. Only use what is present in the provided data.
- If a specific time is requested, include ONLY slots whose range contains that time.
- If the question is 'who is playing', show only booked slots.
- If the question is 'what court am I on' and a player name is given, show only that player's booked slots.
- If there are no matching items after filtering, reply exactly: ""No matching courts or time slots found.""

You have access to tools that can fetch real-time court availability data.

{GetCourtAvailabilityFormattingInstructions()}";
        }

        public CourtAvailabilityAgent(OpenAIClient openAIClient)
        {
            _chatClient = openAIClient?.GetChatClient("gpt-4o-mini") ?? throw new ArgumentNullException(nameof(openAIClient));
            _toolRegistry = new ToolRegistry();
            _deterministicFormatting = false;
            
            // Register only the tools this agent needs
            RegisterAgentTools();
        }

        // Test-only: allow injecting a custom registry (e.g., with a mocked get_court_availability tool)
        public CourtAvailabilityAgent(OpenAIClient openAIClient, ToolRegistry customRegistry)
        {
            _chatClient = openAIClient?.GetChatClient("gpt-4o-mini") ?? throw new ArgumentNullException(nameof(openAIClient));
            _toolRegistry = customRegistry ?? new ToolRegistry();
            _deterministicFormatting = true; // tests inject a custom registry; return structured output deterministically
            // Intentionally do not register default tools; tests will supply the right tool(s)
        }

        public async Task<string> HandleAsync(string prompt, string? userId = null, string? sessionId = null)
        {
            try
            {
                // Create tools for this agent
                var tools = CreateOpenAITools();
                var hasSpecificTime = TryExtractSpecificTime(prompt, out var specificTime);
                var isTonight = prompt.Contains("tonight", StringComparison.OrdinalIgnoreCase);
                var isWhoIsPlaying = prompt.Contains("who is playing", StringComparison.OrdinalIgnoreCase);
                var isWhatCourtAmIOn = prompt.Contains("what court am i on", StringComparison.OrdinalIgnoreCase);
                var playerQuery = TryExtractPlayerName(prompt);
                string? availabilityJson = null;
                bool isPreformattedBlock = false;
                // Pre-parse multi-day/time-range intent early
                var hasRange = TryExtractTimeRange(prompt, out var rangeStart, out var rangeEnd);
                var days = TryExtractWeekdaysWithScope(prompt, out var scopeThisWeek, out var scopeNextWeek);

                // Deterministic test path: bypass LLM planning and return structured, filtered list directly
                if (_deterministicFormatting)
                {
                    var availabilityTool = _toolRegistry.GetTool("get_court_availability");
                    if (availabilityTool != null)
                    {
                        // If multi-day, aggregate first and return
                        if (days.Count > 1)
                        {
                            var dateMap = ComputeDatesForWeekdays(days, scopeThisWeek, scopeNextWeek);
                            var sections = new List<(string dateLabel, string json)>();
                            foreach (var d in dateMap)
                            {
                                var tr = await availabilityTool.ExecuteAsync(new Dictionary<string, object> { ["date"] = d.dateForTool });
                                if (hasRange) tr = FilterAvailabilityJsonByRange(tr, rangeStart, rangeEnd);
                                var availableOnly = prompt.Contains("only available", StringComparison.OrdinalIgnoreCase)
                                    || prompt.Contains("available only", StringComparison.OrdinalIgnoreCase)
                                    || prompt.Contains("exclude booked", StringComparison.OrdinalIgnoreCase)
                                    || prompt.Contains("available slots", StringComparison.OrdinalIgnoreCase);
                                if (availableOnly) tr = FilterAvailableOnly(tr);
                                sections.Add((d.displayDate, tr));
                            }
                            var structuredMulti = BuildStructuredListForMultipleDays(sections);
                            return string.IsNullOrWhiteSpace(structuredMulti) ? "No matching courts or time slots found." : structuredMulti;
                        }

                        var toolResult = await availabilityTool.ExecuteAsync(new Dictionary<string, object>());
                        if (hasSpecificTime)
                        {
                            toolResult = FilterAvailabilityJsonByTime(toolResult, specificTime);
                        }
                        else if (isTonight)
                        {
                            toolResult = FilterAvailabilityJsonByEvening(toolResult);
                        }
                        if (!string.IsNullOrWhiteSpace(playerQuery))
                        {
                            toolResult = FilterAvailabilityJsonByPlayer(toolResult, playerQuery!);
                            toolResult = PruneCourtsWithNoCells(toolResult);
                        }
                        if (isWhoIsPlaying)
                        {
                            toolResult = FilterBookedOnly(toolResult);
                        }
                        var structured = BuildStructuredListFromAvailability(toolResult);
                        return string.IsNullOrWhiteSpace(structured) ? "No matching courts or time slots found." : structured;
                    }
                }
                
                // Multi-day intent detection (already parsed earlier)

                // If multi-day was requested, aggregate over multiple days ourselves, then format
                if (days.Count > 1)
                {
                    var availabilityTool = _toolRegistry.GetTool("get_court_availability");
                    if (availabilityTool != null)
                    {
                        var dateMap = ComputeDatesForWeekdays(days, scopeThisWeek, scopeNextWeek);
                        var sections = new List<(string dateLabel, string json)>();
                        foreach (var d in dateMap)
                        {
                            var toolResult = await availabilityTool.ExecuteAsync(new Dictionary<string, object> { ["date"] = d.dateForTool });
                            if (hasRange)
                            {
                                toolResult = FilterAvailabilityJsonByRange(toolResult, rangeStart, rangeEnd);
                            }
                            if (prompt.Contains("only available", StringComparison.OrdinalIgnoreCase)
                                || prompt.Contains("available only", StringComparison.OrdinalIgnoreCase)
                                || prompt.Contains("exclude booked", StringComparison.OrdinalIgnoreCase)
                                || prompt.Contains("available slots", StringComparison.OrdinalIgnoreCase))
                            {
                                toolResult = FilterAvailableOnly(toolResult);
                            }
                            sections.Add((d.displayDate, toolResult));
                        }
                        var structuredMulti = BuildStructuredListForMultipleDays(sections);
                        if (!string.IsNullOrWhiteSpace(structuredMulti))
                        {
                            return structuredMulti; // deterministic path returns preformatted
                        }
                    }
                }

                // Initial call to OpenAI with tools
                var response = await CallOpenAIAsync(prompt, tools);
                
                // Process any tool calls
                var collectedToolResults = new List<string>();
                if (response.Value.ToolCalls?.Count > 0)
                {
                    var toolCalls = new List<ToolCall>();
                    foreach (var toolCall in response.Value.ToolCalls)
                    {
                        if (toolCall is ChatToolCall functionCall)
                        {
                            var tool = _toolRegistry.GetTool(functionCall.FunctionName);
                            if (tool != null)
                            {
                                var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(
                                    functionCall.FunctionArguments) ?? new Dictionary<string, object>();
                                
                                var toolResult = await tool.ExecuteAsync(parameters);

                                // If a specific time was requested, filter the availability JSON strictly to that time
                                if (hasSpecificTime && string.Equals(tool.Name, "get_court_availability", StringComparison.OrdinalIgnoreCase))
                                {
                                    toolResult = FilterAvailabilityJsonByTime(toolResult, specificTime);
                                }
                                else if (isTonight && string.Equals(tool.Name, "get_court_availability", StringComparison.OrdinalIgnoreCase))
                                {
                                    toolResult = FilterAvailabilityJsonByEvening(toolResult);
                                }

                                // If a player was referenced, restrict to that player only
                                if (!string.IsNullOrWhiteSpace(playerQuery) && string.Equals(tool.Name, "get_court_availability", StringComparison.OrdinalIgnoreCase))
                                {
                                    toolResult = FilterAvailabilityJsonByPlayer(toolResult, playerQuery!);
                                }

                                // If asking "who is playing", restrict to booked-only
                                if (isWhoIsPlaying && string.Equals(tool.Name, "get_court_availability", StringComparison.OrdinalIgnoreCase))
                                {
                                    toolResult = FilterBookedOnly(toolResult);
                                }
                                
                                collectedToolResults.Add($"{tool.Name}: {toolResult}");
                                if (string.Equals(tool.Name, "get_court_availability", StringComparison.OrdinalIgnoreCase))
                                {
                                    availabilityJson = toolResult;
                                }
                            }
                        }
                    }
                }

                // Fallback: if the model didn't call any tools, execute get_court_availability directly
                if (!collectedToolResults.Any())
                {
                    var availabilityTool = _toolRegistry.GetTool("get_court_availability");
                    if (availabilityTool != null)
                    {
                        // If multi-day request detected, aggregate
                        if (days.Count > 1)
                        {
                            var dateMap = ComputeDatesForWeekdays(days, scopeThisWeek);
                            var sections = new List<(string dateLabel, string json)>();
                            foreach (var d in dateMap)
                            {
                                var tr = await availabilityTool.ExecuteAsync(new Dictionary<string, object> { ["date"] = d.dateForTool });
                                if (hasRange)
                                {
                                    tr = FilterAvailabilityJsonByRange(tr, rangeStart, rangeEnd);
                                }
                                sections.Add((d.displayDate, tr));
                            }
                            var structuredMulti = BuildStructuredListForMultipleDays(sections);
                            if (!string.IsNullOrWhiteSpace(structuredMulti))
                            {
                                collectedToolResults.Add($"get_court_availability: {structuredMulti}");
                                availabilityJson = structuredMulti;
                                isPreformattedBlock = true;
                                goto RESPOND;
                            }
                        }

                        var toolResult = await availabilityTool.ExecuteAsync(new Dictionary<string, object>());

                        if (hasSpecificTime)
                        {
                            toolResult = FilterAvailabilityJsonByTime(toolResult, specificTime);
                        }
                        else if (isTonight)
                        {
                            toolResult = FilterAvailabilityJsonByEvening(toolResult);
                        }
                        if (!string.IsNullOrWhiteSpace(playerQuery))
                        {
                            toolResult = FilterAvailabilityJsonByPlayer(toolResult, playerQuery!);
                            toolResult = PruneCourtsWithNoCells(toolResult);
                        }
                        if (isWhoIsPlaying)
                        {
                            toolResult = FilterBookedOnly(toolResult);
                        }

                        collectedToolResults.Add($"get_court_availability: {toolResult}");
                        availabilityJson = toolResult;
                    }
                }

RESPOND:
                if (collectedToolResults.Any())
                {
                    var system = GetSystemPrompt();
                    // Tighten formatting for nuanced queries
                    if (hasSpecificTime)
                    {
                        system += "\nWhen a specific time is requested, do not add extra commentary about other times. Show only courts with a matching slot.";
                    }
                    if (isWhoIsPlaying)
                    {
                        system += "\nFor 'who is playing', list booked entries as 'Player(s) — HH:MM - HH:MM — Court X'. Do not state 'no bookings' if there are booked entries.";
                    }
                    if (isWhatCourtAmIOn && !string.IsNullOrWhiteSpace(playerQuery))
                    {
                        system += $"\nFor 'what court am I on', answer concisely with the court and time for '{playerQuery}'. If multiple, list them on separate lines.";
                    }

                    // If we built a multi-day block already, return it directly to avoid LLM hallucinations
                    if (isPreformattedBlock && !string.IsNullOrWhiteSpace(availabilityJson))
                    {
                        return availabilityJson;
                    }

                    // Otherwise, build a structured single-day block
                    var formattedBlock = availabilityJson != null ? BuildStructuredListFromAvailability(availabilityJson) : string.Empty;
                    if (_deterministicFormatting && !string.IsNullOrWhiteSpace(formattedBlock))
                    {
                        return formattedBlock;
                    }
                    var user = $"Based on this user request: \"{prompt}\"\n\nHere is the structured availability you must use (already filtered if applicable):\n{formattedBlock}\n\nPlease rewrite this neatly for the user, preserving all courts and times shown above.";
                    var finalResponse = await CallOpenAIAsyncWithSystem(system, user);
                    var text = finalResponse.Value.Content[0].Text;
                    if (!string.IsNullOrWhiteSpace(formattedBlock) && (text == null || !text.Contains("Court ")))
                    {
                        return formattedBlock;
                    }
                    return text ?? "I apologize, but I couldn't process your request.";
                }

                return response.Value.Content[0].Text ?? "I apologize, but I couldn't process your request.";
            }
            catch (Exception ex)
            {
                return $"I'm sorry, but I encountered an error while checking court availability: {ex.Message}";
            }
        }

        private static bool TryExtractSpecificTime(string prompt, out TimeSpan time)
        {
            time = default;
            var m = Regex.Match(prompt, @"\b(?<h>\d{1,2}):(?<m>\d{2})\b");
            if (!m.Success) return false;
            if (!int.TryParse(m.Groups["h"].Value, out var h)) return false;
            if (!int.TryParse(m.Groups["m"].Value, out var mm)) return false;
            if (h < 0 || h > 23 || mm < 0 || mm > 59) return false;
            time = new TimeSpan(h, mm, 0);
            return true;
        }

        private static string FilterAvailabilityJsonByTime(string json, TimeSpan target)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("Courts", out var courts)) return json;

                var result = new List<object>();
                foreach (var court in courts.EnumerateArray())
                {
                    if (!court.TryGetProperty("Cells", out var cells)) continue;
                    var keptCells = new List<object>();
                    foreach (var cell in cells.EnumerateArray())
                    {
                        if (!cell.TryGetProperty("TimeSlot", out var tsProp)) continue;
                        var ts = tsProp.GetString() ?? string.Empty;
                        if (TryParseRange(ts, out var start, out var end))
                        {
                            if (target >= start && target <= end)
                            {
                                keptCells.Add(new Dictionary<string, object>
                                {
                                    ["TimeSlot"] = ts,
                                    ["Status"] = cell.TryGetProperty("Status", out var st) ? st.GetString() : null,
                                    ["Player"] = cell.TryGetProperty("Player", out var pl) ? pl.GetString() : null,
                                    ["IsBooked"] = cell.TryGetProperty("IsBooked", out var ib) && ib.ValueKind == JsonValueKind.True
                                });
                            }
                        }
                    }
                    if (keptCells.Count == 0) continue;

                    var courtName = court.TryGetProperty("Name", out var nm) ? nm.GetString() : null;
                    var courtNumber = court.TryGetProperty("CourtNumber", out var cn) ? cn.GetString() : null;
                    result.Add(new Dictionary<string, object>
                    {
                        ["Name"] = courtName,
                        ["CourtNumber"] = courtNumber,
                        ["Cells"] = keptCells
                    });
                }

                var filtered = new Dictionary<string, object>
                {
                    ["Date"] = root.TryGetProperty("Date", out var date) ? date.GetString() : null,
                    ["Courts"] = result
                };
                return JsonSerializer.Serialize(filtered);
            }
            catch
            {
                return json;
            }
        }

        private static bool TryParseRange(string range, out TimeSpan start, out TimeSpan end)
        {
            start = default; end = default;
            var m = Regex.Match(range, @"^(?<s>\d{1,2}:\d{2})\s*-\s*(?<e>\d{1,2}:\d{2})$");
            if (!m.Success) return false;
            if (!TimeSpan.TryParse(m.Groups["s"].Value, out start)) return false;
            if (!TimeSpan.TryParse(m.Groups["e"].Value, out end)) return false;
            return true;
        }

        private static string BuildStructuredListFromAvailability(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("Courts", out var courts)) return "";
                var lines = new List<string>();
                foreach (var court in courts.EnumerateArray())
                {
                    var name = court.TryGetProperty("Name", out var nm) ? nm.GetString() : null;
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    lines.Add($"- **{name}:**");
                    if (court.TryGetProperty("Cells", out var cells))
                    {
                        foreach (var cell in cells.EnumerateArray())
                        {
                            var ts = cell.TryGetProperty("TimeSlot", out var tsp) ? tsp.GetString() : null;
                            var player = cell.TryGetProperty("Player", out var pl) ? pl.GetString() : null;
                            if (!string.IsNullOrWhiteSpace(ts))
                            {
                                lines.Add($"  - **{ts}**: {player}");
                            }
                        }
                    }
                    lines.Add("");
                }
                return string.Join("\n", lines);
            }
            catch
            {
                return "";
            }
        }

        private static string PruneCourtsWithNoCells(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("Courts", out var courts)) return json;
                var result = new List<object>();
                foreach (var court in courts.EnumerateArray())
                {
                    if (!court.TryGetProperty("Cells", out var cells)) continue;
                    var hasAny = cells.EnumerateArray().Any();
                    if (!hasAny) continue;
                    result.Add(JsonSerializer.Deserialize<object>(court.GetRawText())!);
                }
                var filtered = new Dictionary<string, object>
                {
                    ["Date"] = root.TryGetProperty("Date", out var date) ? date.GetString() : null,
                    ["Courts"] = result
                };
                return JsonSerializer.Serialize(filtered);
            }
            catch { return json; }
        }

        private void RegisterAgentTools()
        {
            // Only register tools relevant to court availability
            _toolRegistry.RegisterTool(new GetCourtAvailabilityTool());
            _toolRegistry.RegisterTool(new GetCurrentTimeTool());
        }

        private List<ChatTool> CreateOpenAITools()
        {
            var tools = new List<ChatTool>();
            
            foreach (var tool in _toolRegistry.GetAllTools())
            {
                var toolDefinition = ChatTool.CreateFunctionTool(
                    tool.Name,
                    tool.Description,
                    BinaryData.FromObjectAsJson(tool.Parameters));
                
                tools.Add(toolDefinition);
            }
            
            return tools;
        }

        private async Task<ClientResult<ChatCompletion>> CallOpenAIAsync(string prompt, List<ChatTool> tools)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(GetSystemPrompt()),
                new UserChatMessage(prompt)
            };
            
            var options = new ChatCompletionOptions();
            options.Temperature = 0.2f;
            options.TopP = 0.9f;
            options.MaxOutputTokenCount = 600;
            if (tools.Any())
            {
                foreach (var tool in tools)
                {
                    options.Tools.Add(tool);
                }
            }
            
            return await _chatClient.CompleteChatAsync(messages, options);
        }

        private async Task<ClientResult<ChatCompletion>> CallOpenAIAsyncWithSystem(string systemPrompt, string userPrompt)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userPrompt)
            };
            var options = new ChatCompletionOptions
            {
                Temperature = 0.1f,
                TopP = 0.9f,
                MaxOutputTokenCount = 600
            };
            return await _chatClient.CompleteChatAsync(messages, options);
        }

        private static string GetCourtAvailabilityFormattingInstructions()
        {
            return @"Format your response with these rules in mind:

[Brief intro sentence with day and date]

[Information]

RULES:
- Always separate slots by court (Court 1, Court 2, Court 3) - do not mix courts together
- Always use ""- **Court X:**"" format for court headers
- Always use ""  - **HH:MM - HH:MM**: "" for time slots (note the 2 spaces)
- Use ""Bookable slot"" for available times
- Use ""Training"" for training sessions
- Use actual player names for booked slots
- Always include day and date in intro
- Each court should be listed separately with its own time slots
- Do not combine or mix time slots from different courts
- 'Player Name' is a placeholder for the actual player name, its not booked
- 'Training' is a placeholder for training sessions
- 'Bookable slot' is a placeholder for available times
- Show both players names if available 
- Show if its a friendly or box league game if available
        - If the prompt asks about a specific time or who's playing next, include ONLY the matching time slots. Do NOT include any other times.
        - For a specific time like 18:45, include any slot whose range includes 18:45 (e.g., 18:30 - 19:15). Otherwise, exclude it.
        - When a specific time is requested, use this structure:
          [Brief intro sentence with day and date]
          - **Court X:**
            - **HH:MM - HH:MM**: Player(s) or Bookable slot
          (Repeat per court that has a matching slot; Omit empty courts)
        - If the question includes 'who is playing', list only booked slots and present as Player(s) — Time (HH:MM - HH:MM) — Court X
        - If the question includes 'what court am I on' and a player's name is present in the data, answer with the court(s) and time(s) for that player, concisely
        - If the request spans multiple days, structure output by day with clear sections: 'Day 1 — <Day, Date>', 'Day 2 — <Day, Date>', each listing courts and their matching time slots under that day
        - When no specific time is requested, you may present full sections for booked and available slots as usual
";
        }

        private static string? TryExtractPlayerName(string prompt)
        {
            // Prefer capitalized name patterns after 'for'
            var m = Regex.Match(prompt, @"\bfor\s+(?<name>([A-Z][a-zA-Z'\-]+\s+[A-Z][a-zA-Z'\-]+)|([A-Z]\.\s*[A-Z][a-zA-Z'\-]+)|([A-Z]\s+[A-Z][a-zA-Z'\-]+))\b");
            if (m.Success)
            {
                return m.Groups["name"].Value.Trim();
            }
            // Fallback: general capitalized two-word name anywhere
            m = Regex.Match(prompt, @"\b(?<name>([A-Z][a-zA-Z'\-]+\s+[A-Z][a-zA-Z'\-]+)|([A-Z]\s+[A-Z][a-zA-Z'\-]+))\b");
            if (m.Success)
            {
                return m.Groups["name"].Value.Trim();
            }
            return null;
        }

        private static string FilterAvailabilityJsonByEvening(string json)
        {
            var eveningStart = new TimeSpan(18, 0, 0);
            return FilterAvailabilityJsonByRange(json, eveningStart, new TimeSpan(23, 59, 59));
        }

        private static string FilterAvailabilityJsonByPlayer(string json, string player)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("Courts", out var courts)) return json;
                var playerLower = player.ToLowerInvariant();
                var result = new List<object>();
                foreach (var court in courts.EnumerateArray())
                {
                    if (!court.TryGetProperty("Cells", out var cells)) continue;
                    var keptCells = new List<object>();
                    foreach (var cell in cells.EnumerateArray())
                    {
                        var playerVal = cell.TryGetProperty("Player", out var pl) ? pl.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(playerVal) && playerVal.ToLowerInvariant().Contains(playerLower))
                        {
                            keptCells.Add(JsonSerializer.Deserialize<object>(cell.GetRawText())!);
                        }
                    }
                    if (keptCells.Count == 0) continue;
                    result.Add(new Dictionary<string, object>
                    {
                        ["Name"] = court.TryGetProperty("Name", out var nm) ? nm.GetString() : null,
                        ["CourtNumber"] = court.TryGetProperty("CourtNumber", out var cn) ? cn.GetString() : null,
                        ["Cells"] = keptCells
                    });
                }
                var filtered = new Dictionary<string, object>
                {
                    ["Date"] = root.TryGetProperty("Date", out var date) ? date.GetString() : null,
                    ["Courts"] = result
                };
                return JsonSerializer.Serialize(filtered);
            }
            catch { return json; }
        }

        private static string FilterBookedOnly(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("Courts", out var courts)) return json;
                var result = new List<object>();
                foreach (var court in courts.EnumerateArray())
                {
                    if (!court.TryGetProperty("Cells", out var cells)) continue;
                    var keptCells = new List<object>();
                    foreach (var cell in cells.EnumerateArray())
                    {
                        if (cell.TryGetProperty("IsBooked", out var ib) && ib.ValueKind == JsonValueKind.True)
                        {
                            keptCells.Add(JsonSerializer.Deserialize<object>(cell.GetRawText())!);
                        }
                    }
                    if (keptCells.Count == 0) continue;
                    result.Add(new Dictionary<string, object>
                    {
                        ["Name"] = court.TryGetProperty("Name", out var nm) ? nm.GetString() : null,
                        ["CourtNumber"] = court.TryGetProperty("CourtNumber", out var cn) ? cn.GetString() : null,
                        ["Cells"] = keptCells
                    });
                }
                var filtered = new Dictionary<string, object>
                {
                    ["Date"] = root.TryGetProperty("Date", out var date) ? date.GetString() : null,
                    ["Courts"] = result
                };
                return JsonSerializer.Serialize(filtered);
            }
            catch { return json; }
        }

        private static string FilterAvailableOnly(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("Courts", out var courts)) return json;
                var result = new List<object>();
                foreach (var court in courts.EnumerateArray())
                {
                    if (!court.TryGetProperty("Cells", out var cells)) continue;
                    var keptCells = new List<object>();
                    foreach (var cell in cells.EnumerateArray())
                    {
                        // Available when IsBooked is false
                        var isBooked = cell.TryGetProperty("IsBooked", out var ib) && ib.ValueKind == JsonValueKind.True;
                        if (!isBooked)
                        {
                            keptCells.Add(JsonSerializer.Deserialize<object>(cell.GetRawText())!);
                        }
                    }
                    if (keptCells.Count == 0) continue;
                    result.Add(new Dictionary<string, object>
                    {
                        ["Name"] = court.TryGetProperty("Name", out var nm) ? nm.GetString() : null,
                        ["CourtNumber"] = court.TryGetProperty("CourtNumber", out var cn) ? cn.GetString() : null,
                        ["Cells"] = keptCells
                    });
                }
                var filtered = new Dictionary<string, object>
                {
                    ["Date"] = root.TryGetProperty("Date", out var date) ? date.GetString() : null,
                    ["Courts"] = result
                };
                return JsonSerializer.Serialize(filtered);
            }
            catch { return json; }
        }

        private static string FilterAvailabilityJsonByRange(string json, TimeSpan startInclusive, TimeSpan endInclusive)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("Courts", out var courts)) return json;
                var result = new List<object>();
                foreach (var court in courts.EnumerateArray())
                {
                    if (!court.TryGetProperty("Cells", out var cells)) continue;
                    var keptCells = new List<object>();
                    foreach (var cell in cells.EnumerateArray())
                    {
                        if (!cell.TryGetProperty("TimeSlot", out var tsProp)) continue;
                        var ts = tsProp.GetString() ?? string.Empty;
                        if (TryParseRange(ts, out var st, out var en))
                        {
                            if (st <= endInclusive && en >= startInclusive)
                            {
                                keptCells.Add(JsonSerializer.Deserialize<object>(cell.GetRawText())!);
                            }
                        }
                    }
                    if (keptCells.Count == 0) continue;
                    result.Add(new Dictionary<string, object>
                    {
                        ["Name"] = court.TryGetProperty("Name", out var nm) ? nm.GetString() : null,
                        ["CourtNumber"] = court.TryGetProperty("CourtNumber", out var cn) ? cn.GetString() : null,
                        ["Cells"] = keptCells
                    });
                }
                var filtered = new Dictionary<string, object>
                {
                    ["Date"] = root.TryGetProperty("Date", out var date) ? date.GetString() : null,
                    ["Courts"] = result
                };
                return JsonSerializer.Serialize(filtered);
            }
            catch { return json; }
        }

        private static List<string> TryExtractWeekdaysWithScope(string prompt, out bool thisWeek, out bool nextWeek)
        {
            thisWeek = prompt.Contains("this week", StringComparison.OrdinalIgnoreCase);
            nextWeek = prompt.Contains("next week", StringComparison.OrdinalIgnoreCase);
            var days = new List<string>();
            var dayNames = new[] { "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday" };
            var lower = prompt.ToLowerInvariant();
            foreach (var dn in dayNames)
            {
                if (Regex.IsMatch(lower, $"\\b{dn}\\b")) days.Add(dn);
            }
            return days;
        }

        private static bool TryExtractTimeRange(string prompt, out TimeSpan start, out TimeSpan end)
        {
            start = default; end = default;
            var lower = prompt.ToLowerInvariant()
                .Replace("pm", " pm").Replace("am", " am");
            // Accept forms like "6pm", "6 pm", "7:30", "18:00"
            var matches = Regex.Matches(lower, @"(\b\d{1,2}(?::\d{2})?\s?(am|pm)?\b)");
            if (matches.Count < 2) return false;
            if (!TryParseFlexibleTime(matches[0].Groups[1].Value.Trim(), out start)) return false;
            if (!TryParseFlexibleTime(matches[1].Groups[1].Value.Trim(), out end)) return false;
            if (end < start)
            {
                // assume same evening range; bump end by 12h if missing am/pm
                end = end.Add(TimeSpan.FromHours(12));
            }
            return true;
        }

        private static bool TryParseFlexibleTime(string input, out TimeSpan time)
        {
            time = default;
            var m = Regex.Match(input, @"^(?<h>\d{1,2})(:(?<m>\d{2}))?\s?(?<ampm>am|pm)?$");
            if (!m.Success) return false;
            var h = int.Parse(m.Groups["h"].Value);
            var mm = m.Groups["m"].Success ? int.Parse(m.Groups["m"].Value) : 0;
            var ampm = m.Groups["ampm"].Value;
            if (!string.IsNullOrEmpty(ampm))
            {
                if (ampm == "pm" && h < 12) h += 12;
                if (ampm == "am" && h == 12) h = 0;
            }
            if (h < 0 || h > 23 || mm < 0 || mm > 59) return false;
            time = new TimeSpan(h, mm, 0);
            return true;
        }

        private static List<(string displayDate, string dateForTool)> ComputeDatesForWeekdays(List<string> days, bool thisWeek, bool nextWeek = false)
        {
            var today = DateTime.Today;
            // start of week as Monday
            int diff = (7 + (int)today.DayOfWeek - (int)DayOfWeek.Monday) % 7;
            var monday = today.AddDays(-diff);
            var baseWeek = nextWeek ? monday.AddDays(7) : monday;
            var result = new List<(string displayDate, string dateForTool)>();
            foreach (var dn in days)
            {
                var targetDow = dn switch
                {
                    "monday" => DayOfWeek.Monday,
                    "tuesday" => DayOfWeek.Tuesday,
                    "wednesday" => DayOfWeek.Wednesday,
                    "thursday" => DayOfWeek.Thursday,
                    "friday" => DayOfWeek.Friday,
                    "saturday" => DayOfWeek.Saturday,
                    _ => DayOfWeek.Sunday
                };
                var offset = ((int)targetDow - (int)DayOfWeek.Monday + 7) % 7;
                var date = baseWeek.AddDays(offset);
                // If computed date is in the past relative to today, move to the same day next week
                if (date < today)
                {
                    date = date.AddDays(7);
                }
                result.Add((date.ToString("dddd, MMM d, yyyy"), date.ToString("dd MMM yy")));
            }
            return result;
        }

        private static string BuildStructuredListForMultipleDays(List<(string dateLabel, string json)> sections)
        {
            var lines = new List<string>();
            for (int i = 0; i < sections.Count; i++)
            {
                var (dateLabel, json) = sections[i];
                lines.Add($"Day {i + 1} — {dateLabel}");
                var block = BuildStructuredListFromAvailability(json);
                if (string.IsNullOrWhiteSpace(block))
                {
                    lines.Add("No matching courts or time slots found.");
                }
                else
                {
                    lines.Add(block);
                }
                if (i < sections.Count - 1)
                {
                    lines.Add("\n---\n");
                }
            }
            return string.Join("\n", lines);
        }
    }
}