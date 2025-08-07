using System;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using BookingsApi.Services;

namespace BookingsApi
{
    public class UserMessagesFunction
    {
        private readonly UserMessagesService _service = new();

        [Function("UserHasMessagesFunction")]
        public async Task<HttpResponseData> HasMessages(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "test/messages/has")] HttpRequestData req)
        {
            if (req.Method == "OPTIONS")
            {
                var pre = req.CreateResponse(HttpStatusCode.OK);
                pre.Headers.Add("Access-Control-Allow-Origin", "*");
                pre.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
                pre.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
                return pre;
            }

            try
            {
                var json = await _service.GetUserHasMessagesAsync();
                var res = req.CreateResponse(HttpStatusCode.OK);
                res.Headers.Add("Content-Type", "application/json");
                res.Headers.Add("Access-Control-Allow-Origin", "*");
                await res.WriteStringAsync(json);
                return res;
            }
            catch (Exception ex)
            {
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                err.Headers.Add("Content-Type", "application/json");
                err.Headers.Add("Access-Control-Allow-Origin", "*");
                await err.WriteStringAsync(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
                return err;
            }
        }

        public class MessagesRequest
        {
            public bool? markAsRead { get; set; }
            public bool? showExpired { get; set; }
            public bool? showRead { get; set; }
        }

        [Function("GetUserMessagesFunction")]
        public async Task<HttpResponseData> GetMessages(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "test/messages/inbox")] HttpRequestData req)
        {
            if (req.Method == "OPTIONS")
            {
                var pre = req.CreateResponse(HttpStatusCode.OK);
                pre.Headers.Add("Access-Control-Allow-Origin", "*");
                pre.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
                pre.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
                return pre;
            }

            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var payload = string.IsNullOrWhiteSpace(body) ? new MessagesRequest() : JsonSerializer.Deserialize<MessagesRequest>(body, opts) ?? new MessagesRequest();

                var json = await _service.GetUserMessagesAsync(
                    payload.markAsRead ?? false,
                    payload.showExpired ?? false,
                    payload.showRead ?? true);

                var res = req.CreateResponse(HttpStatusCode.OK);
                res.Headers.Add("Content-Type", "application/json");
                res.Headers.Add("Access-Control-Allow-Origin", "*");
                await res.WriteStringAsync(json);
                return res;
            }
            catch (Exception ex)
            {
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                err.Headers.Add("Content-Type", "application/json");
                err.Headers.Add("Access-Control-Allow-Origin", "*");
                await err.WriteStringAsync(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
                return err;
            }
        }

        [Function("GetSentUserMessagesFunction")]
        public async Task<HttpResponseData> GetSent(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "test/messages/sent")] HttpRequestData req)
        {
            if (req.Method == "OPTIONS")
            {
                var pre = req.CreateResponse(HttpStatusCode.OK);
                pre.Headers.Add("Access-Control-Allow-Origin", "*");
                pre.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
                pre.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
                return pre;
            }

            try
            {
                var json = await _service.GetSentUserMessagesAsync();
                var res = req.CreateResponse(HttpStatusCode.OK);
                res.Headers.Add("Content-Type", "application/json");
                res.Headers.Add("Access-Control-Allow-Origin", "*");
                await res.WriteStringAsync(json);
                return res;
            }
            catch (Exception ex)
            {
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                err.Headers.Add("Content-Type", "application/json");
                err.Headers.Add("Access-Control-Allow-Origin", "*");
                await err.WriteStringAsync(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
                return err;
            }
        }
    }
}


