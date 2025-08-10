using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using OpenAI;
using Xunit;
using BookingsApi.Agents;
using BookingsApi.Tools;
using Bookings.Tests.MockTools;

namespace Bookings.Tests
{
    public class BoxPositionsAgentTests
    {
        private static bool TryGetClient(out OpenAIClient client)
        {
            var key = Environment.GetEnvironmentVariable("OpenAI_API_Key");
            if (string.IsNullOrWhiteSpace(key)) { client = null!; return false; }
            client = new OpenAIClient(key);
            return true;
        }

        [Fact]
        public async Task CardView_Emojis_PerBox_Applied_Correctly()
        {
            if (!TryGetClient(out var client)) { return; }

            // Arrange mock JSON with two boxes, ensuring distinct top3 and last place per box
            var json = JsonSerializer.Serialize(new
            {
                Boxes = new object[]
                {
                    new { Name = "Box A1", Positions = new object[]
                        {
                            new { Pos = 1, Plyr = "Gavin Murphy", Pts = 36, W = 7, L = 1, Pld = 8 },
                            new { Pos = 2, Plyr = "F O'Toole", Pts = 33, W = 5, L = 5, Pld = 10 },
                            new { Pos = 3, Plyr = "R Flannery", Pts = 21, W = 3, L = 3, Pld = 6 },
                            new { Pos = 4, Plyr = "R Cunniffe", Pts = 18, W = 2, L = 3, Pld = 5 },
                            new { Pos = 5, Plyr = "Player Five", Pts = 10, W = 2, L = 2, Pld = 6 },
                            new { Pos = 6, Plyr = "Player Six", Pts = 5, W = 1, L = 4, Pld = 6 }
                        }
                    },
                    new { Name = "Box A2", Positions = new object[]
                        {
                            new { Pos = 1, Plyr = "A Jadoon", Pts = 32, W = 6, L = 2, Pld = 8 },
                            new { Pos = 2, Plyr = "R Fitzsimons", Pts = 20, W = 4, L = 0, Pld = 4 },
                            new { Pos = 3, Plyr = "V Gilcreest", Pts = 11, W = 2, L = 1, Pld = 3 },
                            new { Pos = 4, Plyr = "Name Four", Pts = 7, W = 1, L = 2, Pld = 3 },
                            new { Pos = 5, Plyr = "Name Five", Pts = 5, W = 1, L = 0, Pld = 1 },
                            new { Pos = 6, Plyr = "Name Six", Pts = 0, W = 0, L = 0, Pld = 0 }
                        }
                    }
                }
            });

            var registry = new ToolRegistry();
            registry.RegisterTool(new MockBoxPositionsTool(json));

            var agent = new BoxPositionsAgent(client, registry);

            // Act
            var result = await agent.HandleAsync("Show current box positions for SummerFriendlies");

            // Assert per-box headings
            result.Should().Contain("### Box A1");
            result.Should().Contain("### Box A2");

            // Assert medals in each box (🥇, 🥈, 🥉) and last-place emoji (🥲)
            // Box A1
            result.Should().MatchRegex(@"(?s)### Box A1.*\uD83E\uDD47\s*#?1.*\uD83E\uDD48\s*#?2.*\uD83E\uDD49\s*#?3");
            result.Should().MatchRegex(@"(?s)### Box A1.*\uD83E\uDD72\s*#?6");

            // Box A2
            result.Should().MatchRegex(@"(?s)### Box A2.*\uD83E\uDD47\s*#?1.*\uD83E\uDD48\s*#?2.*\uD83E\uDD49\s*#?3");
            result.Should().MatchRegex(@"(?s)### Box A2.*\uD83E\uDD72\s*#?6");

            // Highlight R Cunniffe with fire
            result.Should().Contain("**R Cunniffe\uD83D\uDD25**");
        }
    }
}


