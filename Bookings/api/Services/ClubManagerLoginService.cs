using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using BookingsApi.Helpers;

namespace BookingsApi.Services
{
    public class ClubManagerLoginService
    {
        private static readonly object SyncRoot = new();
        private static HttpClient? _sharedClient;
        private static HttpClientHandler? _sharedHandler;
        private static DateTime _lastLoginUtc = DateTime.MinValue;
        private static readonly TimeSpan SessionRefreshInterval = TimeSpan.FromMinutes(10);
        private static readonly System.Threading.SemaphoreSlim _loginSemaphore = new(1, 1);

        public async Task<HttpClient> GetAuthenticatedClientAsync()
        {
            // Initialize shared client and handler once
            if (_sharedClient == null)
            {
                lock (SyncRoot)
                {
                    if (_sharedClient == null)
                    {
                        _sharedHandler = new HttpClientHandler
                        {
                            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                            CookieContainer = new CookieContainer()
                        };
                        _sharedClient = new HttpClient(_sharedHandler);
                    }
                }
            }

            // Ensure session is fresh enough
            if (DateTime.UtcNow - _lastLoginUtc > SessionRefreshInterval)
            {
                await _loginSemaphore.WaitAsync();
                try
                {
                    if (DateTime.UtcNow - _lastLoginUtc > SessionRefreshInterval)
                    {
                        // Re-login to refresh cookies/session
                        await new Helpers.LoginHelper4().GetLoggedInRequestAsync(_sharedClient!);
                        _lastLoginUtc = DateTime.UtcNow;
                    }
                }
                finally
                {
                    _loginSemaphore.Release();
                }
            }

            return _sharedClient!;
        }
    }
}


