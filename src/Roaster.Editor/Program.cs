using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Roaster.Editor;
using Roaster.Editor.Services;
using Roaster.Editor.Services.Http;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiOptions = builder.Configuration.GetSection("Api").Get<ApiOptions>()
    ?? throw new InvalidOperationException("Api configuration section is missing.");

if (string.IsNullOrWhiteSpace(apiOptions.BaseUrl))
    throw new InvalidOperationException("Api:BaseUrl is required.");

builder.Services.AddHttpClient("RoasterApi", client =>
    client.BaseAddress = new Uri(apiOptions.BaseUrl));

builder.Services.AddScoped<SubmissionService>();
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<ThreadService>();
builder.Services.AddScoped<IRoastApiService, HttpRoasterService>();

builder.Services.AddMudServices();

await builder.Build().RunAsync();
