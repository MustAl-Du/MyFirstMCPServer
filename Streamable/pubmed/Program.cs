using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore;
using System.Net.Http.Headers;

const int PubMedRequestsPerSecond = 2;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
    .WithToolsFromAssembly();

builder.Services.AddSingleton(_ =>
{
    var handler = new RateLimitedHandler(TimeSpan.FromSeconds(1d / PubMedRequestsPerSecond))
    {
        InnerHandler = new HttpClientHandler()
    };
    var client = new HttpClient(handler) { BaseAddress = new Uri("https://eutils.ncbi.nlm.nih.gov/entrez/eutils/") };
    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("pubmed-tool", "1.0"));
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    return client;
});

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapMcp("/mcp");

await app.RunAsync("http://localhost:3002");

internal sealed class RateLimitedHandler(TimeSpan minimumInterval) : DelegatingHandler
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset nextRequestAt = DateTimeOffset.MinValue;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var delay = nextRequestAt - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            nextRequestAt = DateTimeOffset.UtcNow + minimumInterval;

            return await base.SendAsync(request, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }
}