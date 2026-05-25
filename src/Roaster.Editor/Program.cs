/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Roaster.Editor;
using Roaster.Editor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiOptions = builder.Configuration.GetSection("Api").Get<ApiOptions>()
    ?? throw new InvalidOperationException("Api configuration section is missing.");

if (string.IsNullOrWhiteSpace(apiOptions.BaseUrl))
    throw new InvalidOperationException("Api:BaseUrl is required.");

builder.Services.AddHttpClient<SessionService>(client =>
    client.BaseAddress = new Uri(apiOptions.BaseUrl));

builder.Services.AddHttpClient<SubmissionService>(client =>
    client.BaseAddress = new Uri(apiOptions.BaseUrl));

builder.Services.AddHttpClient<ReviewService>(client =>
    client.BaseAddress = new Uri(apiOptions.BaseUrl));

builder.Services.AddHttpClient<EventService>(client =>
    client.BaseAddress = new Uri(apiOptions.BaseUrl));

builder.Services.AddMudServices();

await builder.Build().RunAsync();
