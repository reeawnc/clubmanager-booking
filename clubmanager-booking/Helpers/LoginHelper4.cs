using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ClubManager.Helpers
{
    public class LoginHelper4
    {
        public LoginHelper4()
        {
        }

        public async Task<Tuple<HttpRequestMessage, HttpResponseMessage>> GetLoggedInRequestAsync(HttpClient client)
        {
            Uri uri = new Uri("https://clubmanager365.com/CourtCalendar.aspx?club=westwood&sport=squash");
            var response = await client.GetAsync(uri);
            string contents = await response.Content.ReadAsStringAsync();
            HttpRequestMessage request = SetupLoginRequest(contents);
            var loginResponse = await client.SendAsync(request);

            Console.WriteLine(loginResponse.Content);
            return new Tuple<HttpRequestMessage, HttpResponseMessage>(request, loginResponse);
        }

        public HttpRequestMessage SetupLoginRequest(string contents)
        {
            var __VIEWSTATE = "";
            var viewState = "";
            var eventValidation = "";
            var eventStateGenerator = "";
            Match match = Regex.Match(contents, "<input type=\"hidden\" name=\"__VIEWSTATE\"[^>]*? value=\"(.*)\"");
            if (match.Success) __VIEWSTATE = match.Groups[1].Value;

            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(contents);

            viewState = doc.DocumentNode.SelectSingleNode("//input[@name='__VIEWSTATE']")
                .Attributes["value"].Value;
            eventValidation = doc.DocumentNode.SelectSingleNode("//input[@name='__EVENTVALIDATION']")
                .Attributes["value"].Value;
            eventStateGenerator = doc.DocumentNode.SelectSingleNode("//input[@name='__VIEWSTATEGENERATOR']")
                .Attributes["value"].Value;

            if (__VIEWSTATE != viewState) throw new Exception("Viewstate is different????");

            var formContent = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("__EVENTARGUMENT", ""),
                        new KeyValuePair<string, string>("__EVENTTARGET", ""),
                        new KeyValuePair<string, string>("__LASTFOCUS", ""),
                        
                        new KeyValuePair<string, string>("__VIEWSTATE", viewState),
                        new KeyValuePair<string, string>("__VIEWSTATEGENERATOR", eventStateGenerator),
                        new KeyValuePair<string, string>("__PREVIOUSPAGE", "wsmp6 - sYGEQr7wPj41v22FYcoeG5rr--gGJY3C8OlYq2sznJryUqBLMFm4_vgh0F5jtwhKnIBJxvIKIUPqTLN4_1FlV9mT7r7Jw8MpZnrxg3QyrV0"),
                        new KeyValuePair<string, string>("__EVENTVALIDATION", eventValidation),
                        new KeyValuePair<string, string>("ctl00$LoginView5$UserLogin$UserName", "rioghan_c@hotmail.com"),
                        new KeyValuePair<string, string>("ctl00$LoginView5$UserLogin$Password", "y7rkLwHbEZCPp2"),
                        new KeyValuePair<string, string>("ctl00$LoginView5$UserLogin$LoginButton", "Login"),
                        new KeyValuePair<string, string>("ctl00$ContentPlaceHolder1$CourtSearch1$BookingDateTextBox", "2 Aug 2023"),
                        new KeyValuePair<string, string>("ctl00$ContentPlaceHolder1$CourtSearch1$DayPartListBox", "3"),
                        new KeyValuePair<string, string>("multiselect_ctl00_ContentPlaceHolder1_CourtSearch1_DayPartListBox", "3"),
                        new KeyValuePair<string, string>("ctl00$ContentPlaceHolder1$CourtSearch1$CourtTypeListBox", "0"),
                        new KeyValuePair<string, string>("multiselect_ctl00_ContentPlaceHolder1_CourtSearch1_CourtTypeListBox", "0")
                    });

            var uri = new Uri("https://clubmanager365.com/CourtCalendar.aspx?club=westwood&sport=squash");
            var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = formContent };

            
            //request.Headers.Add("sec-ch-ua", "\" Not A;Brand\";v=\"99\", \"Chromium\";v=\"101\", \"Google Chrome\";v=\"101\"");
            //request.Headers.Add("sec-ch-ua-mobile", "?0");
            //request.Headers.Add("sec-ch-ua-platform", "\"macOS\"");
            //request.Headers.Add("Upgrade-Insecure-Requests", "1");
            //request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9");
            //request.Headers.Add("Sec-Fetch-Site", "same-origin");
            //request.Headers.Add("Sec-Fetch-Mode", "navigate");
            //request.Headers.Add("Sec-Fetch-User", "?1");
            //request.Headers.Add("Sec-Fetch-Dest", "document");

            return request;
        }
    }
}
