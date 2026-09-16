using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
    .WithToolsFromAssembly();

builder.Services.AddSingleton(_ =>
{
    var client = new HttpClient() { BaseAddress = new Uri("https://clinicaltrials.gov/api/v2/") };
    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("clinicaltrials-tool", "1.0"));
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    return client;
});

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapMcp("/mcp");

await app.RunAsync("http://localhost:3001");