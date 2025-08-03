using ClubManager;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
namespace clubmanager_booking.Biz
{
    public class CourtManager
    {
        public async Task<RootObject> GetCourts(string date, string baseAddress, HttpClient client, ILogger log)
        {
            log.LogInformation("Attempting GetCourts.");
            Uri uri = new Uri("https://clubmanager365.com/CourtCalendar.aspx?club=westwood&sport=squash");
            var response = await client.GetAsync(uri);
            var contents = await response.Content.ReadAsStringAsync();
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

            var param = new Dictionary<string, string>() {
                    { "siteCallback", "CourtCallback" },
                    {"action", "GetCourtDay" },
                    {"", "{'Date':'" + date +"','DayParts':['3'],'CourtTypes':['0'],'SpaceAvailable':1280}"} };

            var newUrl = new Uri(QueryHelpers.AddQueryString(baseAddress, param));
            response = await client.GetAsync(newUrl);
            log.LogInformation($"IsSuccessStatusCode: {response.IsSuccessStatusCode}");
            contents = await response.Content.ReadAsStringAsync();
            RootObject myDeserializedClass = JsonConvert.DeserializeObject<RootObject>(contents);
            var court1 = myDeserializedClass.Courts.FirstOrDefault(c => c.ColumnHeading.StartsWith("Court 1"));
            var court2 = myDeserializedClass.Courts.FirstOrDefault(c => c.ColumnHeading.StartsWith("Court 2"));
            var court3 = myDeserializedClass.Courts.FirstOrDefault(c => c.ColumnHeading.StartsWith("Court 3"));            
            UpdateCellDetails(myDeserializedClass, court1, court2, court3, log);
            return myDeserializedClass;
        }

        public async Task<Cell> GetCourt2Cell(string date, string baseAddress, HttpClient client, ILogger log, int maxRetries = 10, int retryDelay = 1000, string time = "18:00")
        {
            log.LogInformation("Attempting GetCourt2Cell.");
            Cell cell = null;
            int retryCount = 0;
            while (CellIsEmpty(cell) && retryCount < maxRetries)
            {
                log.LogInformation("Attempt " + (retryCount + 1) + " to retrieve valid cell.");
                var root = await GetCourts(date, baseAddress, client, log);
                Court court2 = root.Courts.FirstOrDefault(c => c.ColumnHeading.StartsWith("Court 2"));
                log.LogInformation($"court2: {court2}");
                log.LogInformation($"court2 - cell0: {court2.Cells[0]}");
                log.LogInformation($"court2 - cell0 summary: {court2.Cells[0].Summary}");
                log.LogInformation($"court2 - cell1 summary: {court2.Cells[1].Summary}");
                log.LogInformation($"court2 - cell2 summary: {court2.Cells[2].Summary}");
                log.LogInformation($"court2 - cell3 summary: {court2.Cells[3].Summary}");
                log.LogInformation($"court2 - cell4 summary: {court2.Cells[4].Summary}");
                cell = court2.Cells.FirstOrDefault(x => x.TimeSlot.StartsWith(time));
                log.LogInformation($"cell to book: {cell}");
                log.LogInformation($"cell time slot: {cell.TimeSlot}");
                if (CellIsEmpty(cell))
                {
                    log.LogInformation($"cell is empty: {cell}");
                    cell = null;
                    retryCount++;
                    await Task.Delay(retryDelay);
                }
            }
            log.LogInformation($"Finished GetCourt2Cell");
            log.LogInformation($" ");
            if (CellIsEmpty(cell))
            {                                
                log.LogError($"cell is empty");
                log.LogInformation($"cell: {cell}");                                    
                string errorMessage = "Failed to retrieve valid cell after " + maxRetries + " retries.";
                log.LogError(errorMessage);
                throw new Exception(errorMessage);
            }
            return cell;
        }

        private static bool CellIsEmpty(Cell cell)
        {
            return cell == null || cell?.CourtID == null || cell?.CourtID == 0;
        }
        
        public void UpdateCellDetails(RootObject myDeserializedClass, Court court1, Court court2, Court court3, ILogger log)
        {
            log.LogInformation($"Attempting UpdateCellDetails");
            try { 
                List<Cell> cellsCombined = new List<Cell>();
                for (var i = 0; i < myDeserializedClass.Courts[0].Cells.Count; i++)
                {                    
                    court1.Cells[i].ToolTip = court1.Cells[i].ToolTip.Split('(')[0].Trim();
                    court1.Cells[i].Court = "Court 1";
                    court1.Cells[i].CourtID = court1.CourtID;
                    SetPlayerNames(court1, i, log);

                    court2.Cells[i].ToolTip = court2.Cells[i].ToolTip.Split('(')[0].Trim();
                    court2.Cells[i].Court = "Court 2";
                    court2.Cells[i].CourtID = court2.CourtID;
                    SetPlayerNames(court2, i, log);

                    court3.Cells[i].ToolTip = court3.Cells[i].ToolTip.Split('(')[0].Trim();
                    court3.Cells[i].Court = "Court 3";
                    court3.Cells[i].CourtID = court3.CourtID;
                    SetPlayerNames(court3, i, log);

                    cellsCombined.Add(court1.Cells[i]);
                    cellsCombined.Add(court2.Cells[i]);
                    cellsCombined.Add(court3.Cells[i]);
                }
            }
            catch(Exception ex)
            {
                log.LogError($"Error in UpdateCellDetails: {ex.Message}");
            }
        }

        public void SetPlayerNames(Court court, int i, ILogger log)
        {
            //log.LogInformation($"Attempting SetPlayerNames: {court} - {i}");
            if (court.Cells[i].ToolTip.Contains(" vs "))
            {
                var players = court.Cells[i].ToolTip.Split(" vs ");
                court.Cells[i].Player1 = players[0];
                court.Cells[i].Player2 = players[1];
            }
            else
            {
                court.Cells[i].Player1 = court.Cells[i].ToolTip;
            }
        }
    }
}
