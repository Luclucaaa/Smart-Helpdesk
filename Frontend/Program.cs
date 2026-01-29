using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SmartHelpdesk.Client;
using SmartHelpdesk.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Đăng ký AuthorizationMessageHandler
builder.Services.AddScoped<AuthorizationMessageHandler>();

// Cấu hình HttpClient để gọi Backend API với Authorization header
builder.Services.AddScoped(sp =>
{
    var jsRuntime = sp.GetRequiredService<Microsoft.JSInterop.IJSRuntime>();
    var handler = new AuthorizationMessageHandler(jsRuntime)
    {
        InnerHandler = new HttpClientHandler()
    };
    
    return new HttpClient(handler)
    {
        BaseAddress = new Uri("http://localhost:5001/")
    };
});

// Đăng ký AuthService
builder.Services.AddScoped<AuthService>();

await builder.Build().RunAsync();
