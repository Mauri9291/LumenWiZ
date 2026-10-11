using Xunit;
using LumenWiZ.Core.Server;

namespace LumenWiZ.Tests;

public class LocalServerTests
{
    [Fact]
    public async Task LocalHttpServer_RespondsToStatusEndpoint()
    {
        int testPort = 39876;
        var server = new LocalHttpServer(testPort);

        try
        {
            server.Start();

            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync($"http://localhost:{testPort}/api/status");

            Assert.True(response.IsSuccessStatusCode);
            string body = await response.Content.ReadAsStringAsync();
            Assert.Contains("\"status\":\"ok\"", body);
        }
        finally
        {
            server.Stop();
        }
    }
}
