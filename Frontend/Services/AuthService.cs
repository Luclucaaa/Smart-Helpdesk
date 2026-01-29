using Microsoft.JSInterop;
using System.Net.Http.Headers;

namespace SmartHelpdesk.Client.Services
{
    public class AuthService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly HttpClient _httpClient;

        public AuthService(IJSRuntime jsRuntime, HttpClient httpClient)
        {
            _jsRuntime = jsRuntime;
            _httpClient = httpClient;
        }

        public async Task<string?> GetTokenAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "token");
            }
            catch
            {
                return null;
            }
        }

        public async Task SetTokenAsync(string token)
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "token", token);
        }

        public async Task RemoveTokenAsync()
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "token");
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "userName");
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "userSurname");
        }

        public async Task<bool> IsAuthenticatedAsync()
        {
            var token = await GetTokenAsync();
            return !string.IsNullOrEmpty(token);
        }

        public async Task SetAuthorizationHeaderAsync()
        {
            var token = await GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", token);
            }
        }

        public async Task<string?> GetUserNameAsync()
        {
            try
            {
                var name = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "userName");
                var surname = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "userSurname");
                return $"{name} {surname}".Trim();
            }
            catch
            {
                return null;
            }
        }
    }
}
