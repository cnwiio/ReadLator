using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NovelTranslator.Core.Llm;
using NovelTranslator.Core.Settings;
using Xunit;

namespace NovelTranslator.Tests;

public class FirstRunProbeTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly System.Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(System.Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }

    [Fact]
    public void AppSettings_HasCompletedFirstRun_DefaultsToFalse()
    {
        var settings = new AppSettings();
        Assert.False(settings.HasCompletedFirstRun);
    }

    [Fact]
    public async Task ProbeLocalBackendsAsync_DetectsOllamaFirst()
    {
        using var client = new HttpClient(new StubHandler(req =>
        {
            if (req.RequestUri?.Port == 11434)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"data":[{"id":"qwen2.5:7b"}]}""", Encoding.UTF8, "application/json")
                };
            }
            throw new HttpRequestException("Connection refused");
        }));

        var (backend, baseUrl, firstModel) = await LlmClient.ProbeLocalBackendsAsync(client);

        Assert.Equal(BackendType.Ollama, backend);
        Assert.Equal("http://localhost:11434/v1", baseUrl);
        Assert.Equal("qwen2.5:7b", firstModel);
    }

    [Fact]
    public async Task ProbeLocalBackendsAsync_FallsBackToLmStudio()
    {
        using var client = new HttpClient(new StubHandler(req =>
        {
            if (req.RequestUri?.Port == 1234)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"data":[{"id":"gemma-3-12b"}]}""", Encoding.UTF8, "application/json")
                };
            }
            throw new HttpRequestException("Connection refused");
        }));

        var (backend, baseUrl, firstModel) = await LlmClient.ProbeLocalBackendsAsync(client);

        Assert.Equal(BackendType.LmStudio, backend);
        Assert.Equal("http://localhost:1234/v1", baseUrl);
        Assert.Equal("gemma-3-12b", firstModel);
    }

    [Fact]
    public async Task ProbeLocalBackendsAsync_ReturnsNullWhenNoneRunning()
    {
        using var client = new HttpClient(new StubHandler(_ => throw new HttpRequestException("Connection refused")));

        var (backend, baseUrl, firstModel) = await LlmClient.ProbeLocalBackendsAsync(client);

        Assert.Null(backend);
        Assert.Equal(string.Empty, baseUrl);
        Assert.Null(firstModel);
    }
}