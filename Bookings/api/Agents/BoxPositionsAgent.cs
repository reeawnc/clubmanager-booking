using OpenAI;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using BookingsApi.Tools;
using System.Text;
using System.Text.RegularExpressions;

namespace BookingsApi.Agents
{
    public class BoxPositionsAgent : IAgent
    {
        private readonly ChatClient _chatClient;
        private readonly ToolRegistry _toolRegistry;

        public string Name => "box_positions";
        public string Description => "Handles queries about current box league positions for a given group.";

        public BoxPositionsAgent(OpenAIClient openAIClient)
        {
            _chatClient = openAIClient?.GetChatClient("gpt-4o-mini") ?? throw new ArgumentNullException(nameof(openAIClient));
            _toolRegistry = new ToolRegistry();
            _toolRegistry.RegisterTool(new GetBoxPositionsTool());
        }

        // Test-only: allow injecting a custom tool registry (e.g., with a mocked get_box_positions tool)
        public BoxPositionsAgent(OpenAIClient openAIClient, ToolRegistry customRegistry)
        {
            _chatClient = openAIClient?.GetChatClient("gpt-4o-mini") ?? throw new ArgumentNullException(nameof(openAIClient));
            _toolRegistry = customRegistry ?? new ToolRegistry();
        }

        public async Task<string> HandleAsync(string prompt, string? userId = null, string? sessionId = null)
        {
            // Fast path: parse prompt locally to decide group, call tool directly, then single LLM pass to format
            var (groupParamKey, groupParamValue) = ParseGroupFromPrompt(prompt);
            if (string.IsNullOrEmpty(groupParamKey) || string.IsNullOrEmpty(groupParamValue))
            {
                // If we cannot infer the group, fall back to a helpful message
                return "Please specify which group to show: 'Club' or 'SummerFriendlies', or provide a Group ID.";
            }

            var getBoxTool = _toolRegistry.GetTool("get_box_positions");
            if (getBoxTool == null)
            {
                return "Box positions tool is unavailable right now.";
            }

            var parameters = new Dictionary<string, object> { [groupParamKey] = groupParamValue };
            var toolResultJson = await getBoxTool.ExecuteAsync(parameters);

            // Single formatting pass for compact mobile leaderboard
            var formatMessages = new List<ChatMessage>
            {
                new SystemChatMessage("You will be given box league standings as JSON. Reformat into a compact, mobile-friendly CARD view with these rules:\n\n" +
                    "Parse & normalize: Extract Rank (or Pos), Name (Plyr), Points (Pts as integer), Wins (W), Losses (L), Played (Pld).\n\n" +
                    "Sorting: Sort by Points (desc), then Wins (desc), then Played (desc), then Name (asc, case-insensitive).\n\n" +
                    "Output: For each player output exactly two lines, then a blank line:\n" +
                    "#<rank> **<name>**\n" +
                    "<points>pts | <wins>-<losses>\n\n" +
                    "- Names: keep exactly as given (including apostrophes). Do not truncate names.\n" +
                    "- Align columns so the card is easy to scan; keep lines <= 40 characters where possible.\n" +
                    "- No draws: ignore D for display.\n" +
                    "- Text only. No commentary, no code fences, no extra headers, no trailing spaces.\n" +
                    "- Exactly one blank line between cards; no extra blank lines at start or end.\n\n" +
                    "Optional modifiers (defaults):\n" +
                    "- use_medals=true: For each box independently, prefix ONLY ranks 1, 2, and 3 in that box with medal emojis (\uD83E\uDD47, \uD83E\uDD48, \uD83E\uDD49) while still showing numeric rank, e.g., \"\uD83E\uDD47 #1\". Do not add medals to any other ranks. To disable medals, set use_medals=false.\n" +
                    "- last_place_emoji=\"\uD83E\uDD21\": If non-empty, for each box independently, prefix ONLY the single last-ranked player in that box with this emoji while still showing numeric rank (e.g., \uD83E\uDD21 #12). Do not apply to any other ranks. Set to empty string to disable.\n" +
                    "- highlight_name=\"\": If provided and matches a player name exactly (case-insensitive), append \uD83D\uDD25 immediately after the name inside the bold markers, e.g., **R Cunniffe\uD83D\uDD25**.\n\n" +
                    "If multiple boxes are present, output a minimal markdown heading before each group: '### Box <name>' on its own line, then a blank line, then that box's cards. Maintain exactly one blank line between cards. No trailing blank lines at the end.\n\n" +
                    "Emoji placement rules (MANDATORY): For every box, if use_medals=true then prefix rank 1 with \uD83E\uDD47, rank 2 with \uD83E\uDD48, and rank 3 with \uD83E\uDD49. If last_place_emoji is non-empty, prefix ONLY the lowest rank in that box with that emoji. Do not omit silver or bronze. Do not place emojis on any other ranks."),
                new UserChatMessage($"User settings: {prompt}\n\nBox positions JSON: {toolResultJson}")
            };
            var finalResponse = await _chatClient.CompleteChatAsync(formatMessages);
            var raw = finalResponse.Value.Content[0].Text ?? toolResultJson;
            return NormalizeEmojisAndHighlights(raw);
        }

        // Retained for reference; no longer used
        private static string? TryFormatBoxPositions(string json) => null;

        private static string NormalizeEmojisAndHighlights(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            var lines = input.Split('\n');
            var sections = new List<(int start, int end)>();
            int currentStart = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("### Box "))
                {
                    if (currentStart != -1)
                    {
                        sections.Add((currentStart, i - 1));
                    }
                    currentStart = i;
                }
            }
            if (currentStart != -1)
            {
                sections.Add((currentStart, lines.Length - 1));
            }
            // Fallback: if no explicit box headings were found, treat the entire content as one section
            if (sections.Count == 0 && lines.Length > 0)
            {
                sections.Add((0, lines.Length - 1));
            }

            var rankRegex = new Regex("^\\s*(?:[\\uD83E\\uDD47\\uD83E\\uDD48\\uD83E\\uDD49\\uD83E\\uDD72]\\s*)?#\\s*(?<r>\\d+)", RegexOptions.Compiled);
            string gold = "\uD83E\uDD47";   // 🥇
            string silver = "\uD83E\uDD48"; // 🥈
            string bronze = "\uD83E\uDD49"; // 🥉
            string lastEmoji = "\uD83E\uDD21"; // 🥡 clownish/funny (🥱 alt: use \uD83E\uDD2A zany)

            foreach (var (start, end) in sections)
            {
                var rankLineIndexes = new List<int>();
                var cardNameLineIndexes = new List<int>();
                var statsRegex = new Regex(@"^\s*\d+\s*pts\s*\|\s*\d+\s*-\s*\d+\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
                for (int i = start + 1; i <= end; i++)
                {
                    if (rankRegex.IsMatch(lines[i]))
                    {
                        rankLineIndexes.Add(i);
                    }
                    // Card view: identify name line followed by stats line
                    if (i + 1 <= end && statsRegex.IsMatch(lines[i + 1]))
                    {
                        cardNameLineIndexes.Add(i);
                        i++; // skip the stats line in this pass
                    }
                }
                if (rankLineIndexes.Count == 0) continue;
                // First, strip any pre-existing medal/last emojis from rank lines
                for (int k = 0; k < rankLineIndexes.Count; k++)
                {
                    lines[rankLineIndexes[k]] = StripLeadingEmojis(lines[rankLineIndexes[k]]);
                }
                // Apply medals to first three lines if present
                if (rankLineIndexes.Count >= 1)
                {
                    lines[rankLineIndexes[0]] = PrependIfMissing(lines[rankLineIndexes[0]], gold);
                }
                if (rankLineIndexes.Count >= 2)
                {
                    lines[rankLineIndexes[1]] = PrependIfMissing(lines[rankLineIndexes[1]], silver);
                }
                if (rankLineIndexes.Count >= 3)
                {
                    lines[rankLineIndexes[2]] = PrependIfMissing(lines[rankLineIndexes[2]], bronze);
                }
                // Last place emoji on last rank line
                int lastIdx = rankLineIndexes[rankLineIndexes.Count - 1];
                lines[lastIdx] = PrependIfMissing(lines[lastIdx], lastEmoji, blockMedals: true);

                // Also handle card view (name + stats) when ranks are not present on every line
                if (cardNameLineIndexes.Count > 0)
                {
                    // Strip clown from all card name lines first
                    for (int k = 0; k < cardNameLineIndexes.Count; k++)
                    {
                        var li = cardNameLineIndexes[k];
                        lines[li] = StripLeadingSpecificEmoji(lines[li], lastEmoji);
                    }
                    // Apply clown to the final card's name line
                    var lastCardIdx = cardNameLineIndexes[cardNameLineIndexes.Count - 1];
                    lines[lastCardIdx] = PrependIfMissing(lines[lastCardIdx], lastEmoji, blockMedals: true);
                }
            }

            // Highlight R Cunniffe with a single fire emoji (works with or without bold)
            for (int i = 0; i < lines.Length; i++)
            {
                // Bolded form
                lines[i] = Regex.Replace(
                    lines[i],
                    @"\*\*(?i:R Cunniffe)\*\*",
                    m =>
                    {
                        var nameOnly = m.Value.Substring(2, m.Value.Length - 4);
                        return "**" + nameOnly + "\uD83D\uDD25**";
                    });
                // Plain form (ensure not already followed by fire)
                lines[i] = Regex.Replace(
                    lines[i],
                    @"(?i)\bR Cunniffe\b(?!\uD83D\uDD25)",
                    m => m.Value + "\uD83D\uDD25");
                // Deduplicate any accidental double fire
                lines[i] = lines[i].Replace("\uD83D\uDD25\uD83D\uDD25", "\uD83D\uDD25");
            }

            return string.Join("\n", lines);
        }

        private static string PrependIfMissing(string line, string emoji, bool blockMedals = false)
        {
            var trimmed = line.TrimStart();
            // If already starts with the emoji, return
            if (trimmed.StartsWith(emoji)) return line;
            // If blocking medals on last place and line already has a medal, don't add last-place
            if (blockMedals)
            {
                if (trimmed.StartsWith("\uD83E\uDD47") || trimmed.StartsWith("\uD83E\uDD48") || trimmed.StartsWith("\uD83E\uDD49"))
                {
                    return line;
                }
            }
            // Prepend emoji and a space preserving original indentation
            int indentLen = line.Length - trimmed.Length;
            var indent = indentLen > 0 ? line.Substring(0, indentLen) : string.Empty;
            return indent + emoji + " " + trimmed;
        }

        private static string StripLeadingEmojis(string line)
        {
            // Remove any leading medal or last-place emojis and following spaces
            var pattern = new Regex(@"^(\s*)([\uD83E\uDD47\uD83E\uDD48\uD83E\uDD49\uD83E\uDD72]\s*)+");
            var m = pattern.Match(line);
            if (m.Success)
            {
                return line.Substring(0, m.Groups[1].Length) + line.Substring(m.Length);
            }
            return line;
        }

        private static string StripLeadingSpecificEmoji(string line, string emoji)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith(emoji + " "))
            {
                // Remove exactly one leading emoji and a space
                int indentLen = line.Length - trimmed.Length;
                var indent = indentLen > 0 ? line.Substring(0, indentLen) : string.Empty;
                return indent + trimmed.Substring(emoji.Length + 1);
            }
            return line;
        }

        private static (string key, string value) ParseGroupFromPrompt(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt)) return (string.Empty, string.Empty);
            var text = prompt.ToLowerInvariant();
            if (text.Contains("summerfriendlies") || text.Contains("summer friendlies") || text.Contains("friendlies"))
            {
                return ("group", "SummerFriendlies");
            }
            if (text.Contains("club"))
            {
                return ("group", "Club");
            }
            // Look for a group id number in the text
            var digits = new StringBuilder();
            foreach (var ch in text)
            {
                if (char.IsDigit(ch)) digits.Append(ch);
                else if (digits.Length > 0) break;
            }
            if (digits.Length > 0)
            {
                return ("groupId", digits.ToString());
            }
            return (string.Empty, string.Empty);
        }

        private static int SafeGetInt(JsonElement element)
        {
            try
            {
                return element.ValueKind switch
                {
                    JsonValueKind.Number => element.TryGetInt32(out var i) ? i : (int)element.GetDouble(),
                    JsonValueKind.String => int.TryParse(element.GetString(), out var s) ? s : 0,
                    _ => 0
                };
            }
            catch
            {
                return 0;
            }
        }
    }
}


