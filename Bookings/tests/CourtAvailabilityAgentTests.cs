using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using OpenAI;
using Xunit;
using BookingsApi.Agents;
using BookingsApi.Tools;
using Bookings.Tests.MockTools;

namespace Bookings.Tests
{
    public class CourtAvailabilityAgentTests
    {
        private static bool TryGetClient(out OpenAIClient client)
        {
            var key = Environment.GetEnvironmentVariable("OpenAI_API_Key");
            if (string.IsNullOrWhiteSpace(key)) { client = null!; return false; }
            client = new OpenAIClient(key);
            return true;
        }

        [Fact]
        public async Task Approval_NextWeek_AvailableOnly_MultiDay()
        {
            if (!TryGetClient(out var client)) { return; }

            DateTime today = DateTime.Today;
            int diffToMon = (7 + (int)DayOfWeek.Monday - (int)today.DayOfWeek) % 7;
            var nextMondayDate = today.AddDays(diffToMon + 7);
            var nextTuesdayDate = nextMondayDate.AddDays(1);

            var nextMonday = new
            {
                Date = nextMondayDate.ToString("dd MMM yy"),
                Courts = new object[]
                {
                    new { Name = "Court 1", CourtNumber = "1", Cells = new object[] {
                        new { TimeSlot = "18:00 - 18:30", Status = "available", Player = "Available", IsBooked = false },
                        new { TimeSlot = "18:30 - 19:00", Status = "booked", Player = "X vs Y", IsBooked = true } } },
                }
            };
            var nextTuesday = new
            {
                Date = nextTuesdayDate.ToString("dd MMM yy"),
                Courts = new object[]
                {
                    new { Name = "Court 2", CourtNumber = "2", Cells = new object[] {
                        new { TimeSlot = "18:00 - 18:30", Status = "booked", Player = "A vs B", IsBooked = true },
                        new { TimeSlot = "19:00 - 19:30", Status = "available", Player = "Available", IsBooked = false } } },
                }
            };

            var mapping = new Dictionary<string, string>
            {
                [nextMonday.Date] = System.Text.Json.JsonSerializer.Serialize(nextMonday),
                [nextTuesday.Date] = System.Text.Json.JsonSerializer.Serialize(nextTuesday)
            };
            var registry = new ToolRegistry();
            registry.RegisterTool(new MockCourtAvailabilityTool(mapping));

            var agent = new CourtAvailabilityAgent(client, registry);

            var prompt = "Show only available slots between 6pm and 7:30 for Monday and Tuesday next week";
            var result = await agent.HandleAsync(prompt);

            result.Should().Contain("Day 1");
            result.Should().Contain("Day 2");
            result.Should().Contain("18:00 - 18:30");
            // Only available slots should be present; ensure booked windows like 19:00 - 19:30 are excluded in available-only scenario
            result.Should().NotContain("X vs Y");
            result.Should().NotContain("A vs B");
        }
        [Fact]
        public async Task Approval_TodayAfter5pm_WithMockedData()
        {
            if (!TryGetClient(out var client)) { return; }

            // Arrange: mocked payload includes two booked and two available entries across Court 1 and Court 2
            var mockedJson =
                new
                {
                    Date = "08 Aug 25",
                    Courts = new object[]
                    {
                        new
                        {
                            Name = "Court 1",
                            CourtNumber = "1",
                            Cells = new object[]
                            {
                                new { TimeSlot = "18:00 - 18:45", Status = "booked", Player = "Alice", IsBooked = true, Court = "Court 1" },
                                new { TimeSlot = "18:45 - 19:30", Status = "available", Player = "Available", IsBooked = false, Court = "Court 1" }
                            }
                        },
                        new
                        {
                            Name = "Court 2",
                            CourtNumber = "2",
                            Cells = new object[]
                            {
                                new { TimeSlot = "19:00 - 19:30", Status = "booked", Player = "Bob vs Carol", IsBooked = true, Court = "Court 2" },
                                new { TimeSlot = "19:30 - 20:15", Status = "available", Player = "Available", IsBooked = false, Court = "Court 2" }
                            }
                        }
                    }
                };
            var mockedJsonStr = System.Text.Json.JsonSerializer.Serialize(mockedJson);

            var registry = new ToolRegistry();
            registry.RegisterTool(new MockCourtAvailabilityTool(mockedJsonStr));

            var agent = new CourtAvailabilityAgent(client, registry);

            // Prompt (approval style)
            var prompt = "Show me the court timetable for today after 5pm";
            var result = await agent.HandleAsync(prompt);

            // Expected (approval style) — strict, readable structure
            result.Should().Contain("Court 1");
            result.Should().Contain("Court 2");
            result.Should().Contain("18:00 - 18:45");
            result.Should().Contain("19:00 - 19:30");
            result.Should().Contain("Alice");
            result.Should().Contain("Bob");
        }

        [Fact]
        public async Task Approval_SpecificTime_1845_OnlyMatchingSlots()
        {
            if (!TryGetClient(out var client)) { return; }

            // Court 1 has 18:15-19:00 (matches 18:45), Court 2 has 19:00-19:45 (does not include 18:45)
            var mockedJson2 = new
            {
                Date = "08 Aug 25",
                Courts = new object[]
                {
                    new
                    {
                        Name = "Court 1",
                        CourtNumber = "1",
                        Cells = new object[]
                        {
                            new { TimeSlot = "18:15 - 19:00", Status = "booked", Player = "Dave", IsBooked = true, Court = "Court 1" }
                        }
                    },
                    new
                    {
                        Name = "Court 2",
                        CourtNumber = "2",
                        Cells = new object[]
                        {
                            new { TimeSlot = "19:00 - 19:45", Status = "available", Player = "Available", IsBooked = false, Court = "Court 2" }
                        }
                    }
                }
            };
            var mockedJsonStr2 = System.Text.Json.JsonSerializer.Serialize(mockedJson2);

            var registry = new ToolRegistry();
            registry.RegisterTool(new MockCourtAvailabilityTool(mockedJsonStr2));
            var agent = new CourtAvailabilityAgent(client, registry);

            var prompt = "I'm looking for bookings at 18:45";
            var result = await agent.HandleAsync(prompt);

            result.Should().Contain("Court 1");
            result.Should().Contain("18:15 - 19:00");
            result.Should().NotContain("19:00 - 19:45");
            result.Should().NotContain("Court 2");

        }

        [Fact]
        public async Task Approval_PerCourt_Filter_TimeOverlap_MultipleCourts()
        {
            if (!TryGetClient(out var client)) { return; }

            // Court 1 and Court 3 have slots spanning 18:45; Court 2 has non-overlapping slots
            var mockedJson = new
            {
                Date = "08 Aug 25",
                Courts = new object[]
                {
                    new { Name = "Court 1", CourtNumber = "1", Cells = new object[] { new { TimeSlot = "18:30 - 19:15", Status = "booked", Player = "Eve", IsBooked = true, Court = "Court 1" } } },
                    new { Name = "Court 2", CourtNumber = "2", Cells = new object[] { new { TimeSlot = "17:30 - 18:15", Status = "booked", Player = "Frank", IsBooked = true, Court = "Court 2" } } },
                    new { Name = "Court 3", CourtNumber = "3", Cells = new object[] { new { TimeSlot = "18:45 - 19:30", Status = "available", Player = "Available", IsBooked = false, Court = "Court 3" } } }
                }
            };
            var mockedJsonStr = System.Text.Json.JsonSerializer.Serialize(mockedJson);

            var registry = new ToolRegistry();
            registry.RegisterTool(new MockCourtAvailabilityTool(mockedJsonStr));
            var agent = new CourtAvailabilityAgent(client, registry);

            var prompt = "Looking for 18:45 availability today";
            var result = await agent.HandleAsync(prompt);

            result.Should().Contain("Court 1");
            result.Should().Contain("18:30 - 19:15");
            result.Should().Contain("Court 3");
            result.Should().Contain("18:45 - 19:30");
            result.Should().NotContain("Court 2");
            result.Should().NotContain("17:30 - 18:15");
        }

        [Fact]
        public async Task Approval_MultiDay_TimeRange_ThisWeek()
        {
            if (!TryGetClient(out var client)) { return; }

            // Build two days of deterministic data (Monday and Tuesday)
            // Only include times that overlap 18:00–19:30
            var monday = new
            {
                Date = DateTime.Today.AddDays(((int)DayOfWeek.Monday - (int)DateTime.Today.DayOfWeek + 7) % 7).ToString("dd MMM yy"),
                Courts = new object[]
                {
                    new { Name = "Court 1", CourtNumber = "1", Cells = new object[] { new { TimeSlot = "18:00 - 18:30", Status = "available", Player = "Available", IsBooked = false }, new { TimeSlot = "18:30 - 19:00", Status = "available", Player = "Available", IsBooked = false } } },
                    new { Name = "Court 2", CourtNumber = "2", Cells = new object[] { new { TimeSlot = "18:00 - 18:30", Status = "booked", Player = "A vs B", IsBooked = true }, new { TimeSlot = "19:00 - 19:30", Status = "available", Player = "Available", IsBooked = false } } }
                }
            };
            var tuesday = new
            {
                Date = DateTime.Today.AddDays(((int)DayOfWeek.Tuesday - (int)DateTime.Today.DayOfWeek + 7) % 7).ToString("dd MMM yy"),
                Courts = new object[]
                {
                    new { Name = "Court 3", CourtNumber = "3", Cells = new object[] { new { TimeSlot = "18:00 - 18:30", Status = "available", Player = "Available", IsBooked = false }, new { TimeSlot = "19:00 - 19:30", Status = "booked", Player = "C vs D", IsBooked = true } } }
                }
            };

            // Registry that returns the right day's payload depending on 'date'
            var registry = new ToolRegistry();
            var mapping = new Dictionary<string, string>
            {
                [monday.Date] = System.Text.Json.JsonSerializer.Serialize(monday),
                [tuesday.Date] = System.Text.Json.JsonSerializer.Serialize(tuesday)
            };
            registry.RegisterTool(new MockCourtAvailabilityTool(mapping));

            var agent = new CourtAvailabilityAgent(client, registry);

            var prompt = "Show me the court timetable between 6pm and 7:30 for Monday and Tuesday this week";
            var result = await agent.HandleAsync(prompt);

            result.Should().Contain("Day 1");
            result.Should().Contain("Day 2");
            result.Should().Contain("18:00 - 18:30");
        }
        [Fact]
        public async Task Approval_WhoIsPlaying_Tonight_ShowsBookedPlayersOnly()
        {
            if (!TryGetClient(out var client)) { return; }

            var mockedJson3 = new
            {
                Date = "08 Aug 25",
                Courts = new object[]
                {
                    new { Name = "Court 1", CourtNumber = "1", Cells = new object[] { new { TimeSlot = "17:45 - 18:30", Status = "booked", Player = "Alice vs Bob", IsBooked = true }, new { TimeSlot = "18:30 - 19:15", Status = "available", Player = "Available", IsBooked = false } } },
                    new { Name = "Court 2", CourtNumber = "2", Cells = new object[] { new { TimeSlot = "19:15 - 20:00", Status = "booked", Player = "Carol vs Dan", IsBooked = true } } }
                }
            };
            var mockedJsonStr3 = System.Text.Json.JsonSerializer.Serialize(mockedJson3);

            var registry = new ToolRegistry();
            registry.RegisterTool(new MockCourtAvailabilityTool(mockedJsonStr3));
            var agent = new CourtAvailabilityAgent(client, registry);

            var prompt = "Who is playing tonight?";
            var result = await agent.HandleAsync(prompt);

            result.Should().Contain("Alice");
            result.Should().Contain("Bob");
            result.Should().Contain("Carol");
            result.Should().Contain("Dan");
            result.Should().NotContain("Available");
        }

        [Fact]
        public async Task Approval_WhatCourtAmIOn_Tonight_ForSpecificPlayer()
        {
            if (!TryGetClient(out var client)) { return; }

            // include multiple entries; we expect only R Cunniffe to be reported
            var mockedJson4 = new
            {
                Date = "08 Aug 25",
                Courts = new object[]
                {
                    new { Name = "Court 1", CourtNumber = "1", Cells = new object[] { 
                        new { TimeSlot = "18:00 - 18:45", Status = "booked", Player = "R Cunniffe vs J Doe", IsBooked = true }, 
                        new { TimeSlot = "18:45 - 19:30", Status = "booked", Player = "Someone Else", IsBooked = true } } },
                    new { Name = "Court 3", CourtNumber = "3", Cells = new object[] { new { TimeSlot = "19:30 - 20:15", Status = "booked", Player = "R Cunniffe vs Another", IsBooked = true } } }
                }
            };
            var mockedJsonStr4 = System.Text.Json.JsonSerializer.Serialize(mockedJson4);

            var registry = new ToolRegistry();
            registry.RegisterTool(new MockCourtAvailabilityTool(mockedJsonStr4));
            var agent = new CourtAvailabilityAgent(client, registry);

            var prompt = "What court am I on tonight for R Cunniffe?";
            var result = await agent.HandleAsync(prompt);

            result.Should().Contain("R Cunniffe");
            result.Should().Contain("Court 1");
            result.Should().Contain("Court 3");
            result.Should().NotContain("Someone Else");
        }
    }
}


