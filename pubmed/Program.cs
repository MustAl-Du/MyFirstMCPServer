using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;
using System.Net.Http.Headers;

var builder = Host.CreateEmptyApplicationBuilder(settings: null);

builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

builder.Services.AddSingleton(_ =>
{
    var handler = new RateLimitedHandler(TimeSpan.FromMilliseconds(500))
    {
        InnerHandler = new HttpClientHandler()
    };
    var client = new HttpClient(handler) { BaseAddress = new Uri("https://eutils.ncbi.nlm.nih.gov/entrez/eutils/") };
    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("pubmed-tool", "1.0"));
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    return client;
});

var app = builder.Build();

await app.RunAsync();

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
        }
        finally
        {
            gate.Release();
        }

        return await base.SendAsync(request, cancellationToken);
    }
}