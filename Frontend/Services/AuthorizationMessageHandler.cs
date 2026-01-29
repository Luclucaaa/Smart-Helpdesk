using Microsoft.JSInterop;
using System.Net.Http.Headers;

namespace SmartHelpdesk.Client.Services
{
    public class AuthorizationMessageHandler : DelegatingHandler
    {
        private readonly IJSRuntime _jsRuntime;

        public AuthorizationMessageHandler(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, 
            CancellationToken cancellationToken)
        {
            try
            {
                var token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "token");
                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
            }
            catch
            {
                // JSRuntime không khả dụng (pre-render), bỏ qua
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
