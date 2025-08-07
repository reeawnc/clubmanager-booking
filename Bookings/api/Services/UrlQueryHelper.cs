using System;
using System.Collections.Generic;
using System.Text;

namespace BookingsApi.Services
{
    internal static class UrlQueryHelper
    {
        public static string BuildUrl(string baseUrl, Dictionary<string, string> parameters)
        {
            var sb = new StringBuilder(baseUrl);
            if (!baseUrl.Contains("?"))
            {
                sb.Append('?');
            }

            var first = baseUrl.Contains("?") && baseUrl.IndexOf('?') < baseUrl.Length - 1;
            foreach (var kvp in parameters)
            {
                if (first)
                {
                    sb.Append('&');
                }
                first = true;

                if (string.IsNullOrEmpty(kvp.Key))
                {
                    // Append raw value (e.g., JSON like &{"BookingID":123})
                    sb.Append(kvp.Value);
                }
                else
                {
                    sb.Append(Uri.EscapeDataString(kvp.Key));
                    sb.Append('=');
                    sb.Append(Uri.EscapeDataString(kvp.Value));
                }
            }

            return sb.ToString();
        }
    }
}


