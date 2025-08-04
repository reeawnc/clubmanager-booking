using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using BookingsApi.Services;
using BookingsApi.Models;
using System.Linq;
using Sentry;

namespace BookingsApi.Tools
{
    public class BookCourtTool : ITool
    {
        public string Name => "book_court";
        
        public string Description => "Book a squash court for a specific date and time. This tool will first check court availability, then book the best available court (Court 1 preferred, then Court 2, then Court 3).";
        
        public Dictionary<string, object> Parameters => new()
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["date"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Date to book the court (format: 'dd MMM yy' like '01 Jan 25'). Optional - defaults to today if not provided."
                },
                ["time"] = new Dictionary<string, object>
                {
                    ["type"] = "string", 
                    ["description"] = "Time slot to book (format: 'HH:MM' like '18:00' for 6pm). Required."
                },
                ["duration"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Duration of booking in minutes. Optional - defaults to 60 minutes."
                }
            },
            ["required"] = new[] { "time" }
        };
        
        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            try
            {
                // Extract parameters
                var date = parameters.TryGetValue("date", out var dateValue) 
                    ? dateValue?.ToString() 
                    : DateTime.Now.ToString("dd MMM yy");
                    
                var time = parameters.TryGetValue("time", out var timeValue) 
                    ? timeValue?.ToString() 
                    : throw new ArgumentException("Time parameter is required");
                    
                var duration = parameters.TryGetValue("duration", out var durationValue) 
                    ? int.Parse(durationValue?.ToString() ?? "60") 
                    : 60;

                // Step 1: Get court availability to find available slots
                var courtAvailabilityService = new CourtAvailabilityService();
                var courtData = await courtAvailabilityService.GetCourtAvailabilityAsync(date);
                
                // Step 2: Find the best available court and slot
                var bookingResult = await FindAndBookBestCourt(courtData, date, time, duration);
                
                return bookingResult;
            }
            catch (Exception ex)
            {
                return $"Error booking court: {ex.Message}";
            }
        }
        
        private async Task<string> FindAndBookBestCourt(RootObject courtData, string date, string time, int duration)
        {
            var debugInfo = new List<string>();
            debugInfo.Add($"=== BOOKING TOOL DEBUG ===");
            debugInfo.Add($"Looking for time: {time}");
            debugInfo.Add($"Date: {date}");
            
            // Define court preference order (Court 1 → Court 2 → Court 3)
            var courtPreferences = new[] { "Court 1", "Court 2", "Court 3" };
            
            foreach (var preferredCourt in courtPreferences)
            {
                debugInfo.Add($"\n--- Checking {preferredCourt} ---");
                
                var court = courtData.Courts.FirstOrDefault(c => c.ColumnHeading.StartsWith(preferredCourt));
                if (court == null) 
                {
                    debugInfo.Add($"Court not found: {preferredCourt}");
                    continue;
                }
                
                debugInfo.Add($"Found court: {court.ColumnHeading}");
                debugInfo.Add($"CourtID: {court.CourtID}");
                debugInfo.Add($"Total cells: {court.Cells.Count}");
                
                // Check all slots for this court
                foreach (var cell in court.Cells)
                {
                    debugInfo.Add($"  Slot: {cell.TimeSlot}");
                    debugInfo.Add($"    Player: {cell.Player1 ?? "null"}");
                    debugInfo.Add($"    ToolTip: {cell.ToolTip ?? "null"}");
                    debugInfo.Add($"    Status: {cell.CssClass ?? "null"}");
                    debugInfo.Add($"    Contains time '{time}': {cell.TimeSlot.Contains(time)}");
                    debugInfo.Add($"    ToolTip contains 'Bookable slot': {cell.ToolTip?.Contains("Bookable slot")}");
                    debugInfo.Add($"    ToolTip contains 'Available': {cell.ToolTip?.Contains("Available")}");
                    debugInfo.Add($"    Player1 contains 'Book this slot': {cell.Player1?.Contains("Book this slot")}");
                }
                
                // Find available slot at the requested time
                var availableSlot = court.Cells.FirstOrDefault(cell => 
                    cell.TimeSlot.Contains(time) && 
                    (cell.ToolTip?.Contains("Bookable slot") == true || 
                     cell.ToolTip?.Contains("Available") == true || 
                     cell.Player1?.Contains("Book this slot") == true));
                
                if (availableSlot != null)
                {
                    debugInfo.Add($"✅ Found available slot: {availableSlot.TimeSlot}");
                    debugInfo.Add($"CourtSlotID: {availableSlot.CourtSlotID}");
                    
                    // Found an available slot! Now book it
                    var bookingResult = await MakeBookingRequest(court.CourtID.ToString(), availableSlot.CourtSlotID.ToString(), date, time);
                    return $"{bookingResult}\n\n{string.Join("\n", debugInfo)}";
                }
                else
                {
                    debugInfo.Add($"❌ No available slot found for {preferredCourt} at {time}");
                }
            }
            
            debugInfo.Add($"\n=== END BOOKING TOOL DEBUG ===");
            return $"No available courts found for {time} on {date}. All courts are either booked or unavailable.\n\n{string.Join("\n", debugInfo)}";
        }
        
        private async Task<string> MakeBookingRequest(string courtID, string courtSlotID, string date, string time)
        {
            try
            {
                // Format date for booking (convert from "dd MMM yy" to "d MMM yyyy")
                var bookingDate = DateTime.ParseExact(date, "dd MMM yy", null).ToString("d MMM yyyy");
                
                CookieContainer cookies = new CookieContainer();
                HttpClientHandler handler = new HttpClientHandler()
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                    CookieContainer = cookies,
                };
                
                using (var client = new HttpClient(handler))
                {
                    // Step 1: Login to get authenticated session
                    await PerformLogin(client);
                    
                    // Step 2: Make the booking request
                    var param = new Dictionary<string, string>() {
                        { "siteCallback", "CourtCallback" },
                        {"action", "MakeBooking" }
                    };

                    var url = QueryHelpers.AddQueryString("https://clubmanager365.com/Club/ActionHandler.ashx", param);
                    url = url + "&{\"OpponentPlayerIDs\":null,\"CourtsRequired\":[{\"c\":\"" + courtID + "\",\"s\":\"" + courtSlotID + "\"}],\"Notification\":\"-1\",\"Resources\":[],\"MatchDate\":\"" + bookingDate + "\",\"ExpectedBalanceAmount\":\"\",\"PaymentAmount\":0,\"SelectedMatchType\":\"4\",\"ExtensionCourtSlotID\":\"0\",\"CourtID\":\"" + courtID + "\",\"PackageItem1\":\"\",\"PackageItem2\":\"\",\"PackageItem3\":\"\"}";
                    
                    var uri = new Uri(url);
                    var bookingsResponse = await client.GetAsync(uri);
                    var contents = await bookingsResponse.Content.ReadAsStringAsync();
                    
                    // Parse the response to check if booking was successful
                    var response = JsonConvert.DeserializeObject<dynamic>(contents);
                    
                    if (response.success == true)
                    {
                        return $"Successfully booked court for {time} on {date}. Booking confirmed!";
                    }
                    else
                    {
                        return $"Booking failed: {response.message ?? "Unknown error"}";
                    }
                }
            }
            catch (Exception ex)
            {
                return $"Error making booking request: {ex.Message}";
            }
        }
        
        private async Task PerformLogin(HttpClient client)
        {
            // Get the login page to extract form values
            Uri uri = new Uri("https://clubmanager365.com/CourtCalendar.aspx?club=westwood&sport=squash");
            var response = await client.GetAsync(uri);
            var contents = await response.Content.ReadAsStringAsync();

            // Extract form values using regex
            var __VIEWSTATE = "";
            var __VIEWSTATEGENERATOR = "";
            var __PREVIOUSPAGE = "";
            var __EVENTVALIDATION = "";
            
            Match match = Regex.Match(contents, "<input type=\"hidden\" name=\"__VIEWSTATE\"[^>]*? value=\"(.*)\"");
            if (match.Success) __VIEWSTATE = match.Groups[1].Value;

            match = Regex.Match(contents, "<input type=\"hidden\" name=\"__VIEWSTATEGENERATOR\"[^>]*? value=\"(.*)\"");
            if (match.Success) __VIEWSTATEGENERATOR = match.Groups[1].Value;

            match = Regex.Match(contents, "<input type=\"hidden\" name=\"__PREVIOUSPAGE\"[^>]*? value=\"(.*)\"");
            if (match.Success) __PREVIOUSPAGE = match.Groups[1].Value;

            match = Regex.Match(contents, "<input type=\"hidden\" name=\"__EVENTVALIDATION\"[^>]*? value=\"(.*)\"");
            if (match.Success) __EVENTVALIDATION = match.Groups[1].Value;

            // Create login form data
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__EVENTARGUMENT", ""),
                new KeyValuePair<string, string>("__EVENTTARGET", ""),
                new KeyValuePair<string, string>("__LASTFOCUS", ""),
                new KeyValuePair<string, string>("__VIEWSTATE", __VIEWSTATE),
                new KeyValuePair<string, string>("__VIEWSTATEGENERATOR", __VIEWSTATEGENERATOR),
                new KeyValuePair<string, string>("__PREVIOUSPAGE", __PREVIOUSPAGE),
                new KeyValuePair<string, string>("__EVENTVALIDATION", __EVENTVALIDATION),
                new KeyValuePair<string, string>("ctl00$LoginView5$UserLogin$UserName", "rioghan_c@hotmail.com"),
                new KeyValuePair<string, string>("ctl00$LoginView5$UserLogin$Password", "y7rkLwHbEZCPp2"),
                new KeyValuePair<string, string>("ctl00$LoginView5$UserLogin$LoginButton", "Login"),
                new KeyValuePair<string, string>("ctl00$ContentPlaceHolder1$CourtSearch1$BookingDateTextBox", "2 Aug 2023"),
                new KeyValuePair<string, string>("ctl00$ContentPlaceHolder1$CourtSearch1$DayPartListBox", "3"),
                new KeyValuePair<string, string>("multiselect_ctl00_ContentPlaceHolder1_CourtSearch1_DayPartListBox", "3"),
                new KeyValuePair<string, string>("ctl00$ContentPlaceHolder1$CourtSearch1$CourtTypeListBox", "0"),
                new KeyValuePair<string, string>("multiselect_ctl00_ContentPlaceHolder1_CourtSearch1_CourtTypeListBox", "0")
            });

            // Submit login form
            var loginUri = new Uri("https://clubmanager365.com/CourtCalendar.aspx?club=westwood&sport=squash");
            var loginRequest = new HttpRequestMessage(HttpMethod.Post, loginUri) { Content = formContent };
            await client.SendAsync(loginRequest);
        }
    }
} 