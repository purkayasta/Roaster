using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Editor;
using Editor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddMudServices();

// DEPENDENCY INJECTION: Register the Roast API Service
// This is where the magic happens! We're telling Blazor to use MockRoastApiService
// whenever a component requests IRoastApiService.
//
// To switch to a real backend service later, just change MockRoastApiService to HttpRoastApiService!
// Components don't need to change at all.
builder.Services.AddScoped<IRoastApiService, MockRoastApiService>();

await builder.Build().RunAsync();
