using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NovelTranslator.Core.Settings;

namespace NovelTranslator.Core.Llm;

public class LlmClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly AppSettings _settings;

    public LlmClient(AppSettings settings, HttpClient? httpClient = null)
    {
        _settings = settings;
        if (httpClient == null)
        {
            int timeoutSeconds = Math.Clamp(_settings.RequestTimeoutSeconds > 0 ? _settings.RequestTimeoutSeconds : 300, 10, 3600);
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) }; // เผื่อโมเดลขนาดใหญ่ประมวลผลนาน
        }
        else
        {
            _httpClient = httpClient;
        }
    }

    internal string BuildEndpointUrl(string suffix)
    {
        string baseUrl = (_settings.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("กรุณาระบุ Base URL ของผู้ให้บริการ AI ในหน้าตั้งค่าก่อนใช้งาน");
        }
        if (baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            return $"{baseUrl}{suffix}";
        }
        return $"{baseUrl}/v1{suffix}";
    }

    private string GetCompletionsUrl() => BuildEndpointUrl("/chat/completions");

    private string GetModelsUrl() => BuildEndpointUrl("/models");

    private void ApplyHeaders(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        }
    }

    public IAsyncEnumerable<string> StreamChatAsync(
        IEnumerable<ChatMessage> messages,
        float temperature,
        CancellationToken cancellationToken = default)
        => StreamChatAsync(messages, temperature, null, cancellationToken);

    public Dictionary<string, object> BuildRequestPayload(
        string targetModel,
        IEnumerable<ChatMessage> messages,
        float temperature)
    {
        var payload = new Dictionary<string, object>
        {
            ["model"] = targetModel,
            ["messages"] = messages,
            ["temperature"] = Math.Round(temperature, 2),
            ["stream"] = true
        };

        if (_settings.TopP is > 0f and < 0.999f)
        {
            payload["top_p"] = Math.Round(_settings.TopP, 2);
        }
        if (_settings.MaxTokens > 0)
        {
            payload["max_tokens"] = _settings.MaxTokens;
        }
        if (Math.Abs(_settings.FrequencyPenalty) > 0.001f)
        {
            payload["frequency_penalty"] = Math.Round(_settings.FrequencyPenalty, 2);
        }

        return payload;
    }

    public async IAsyncEnumerable<string> StreamChatAsync(
        IEnumerable<ChatMessage> messages,
        float temperature,
        string? modelOverride,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string targetModel = !string.IsNullOrWhiteSpace(modelOverride)
            ? modelOverride.Trim()
            : (_settings.ModelName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(targetModel))
        {
            throw new InvalidOperationException("กรุณาเลือกโมเดล AI ในหน้าตั้งค่าก่อนเริ่มแปล");
        }

        string url = GetCompletionsUrl();
        var requestBody = BuildRequestPayload(targetModel, messages, temperature);

        string json = JsonSerializer.Serialize(requestBody);
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        ApplyHeaders(request);

        using HttpResponseMessage response = await SendChatRequestAsync(request, url, json, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"AI Server ตอบกลับสถานะข้อผิดพลาด {(int)response.StatusCode}: {errorBody}\n\n[Debug Info]\nEndpoint: {url}\nPayload:\n{json}");
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? line = await reader.ReadLineAsync(cancellationToken);
            if (line == null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            line = line.Trim();
            if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                string data = line.Substring(5).Trim();
                if (data == "[DONE]") break;

                string? chunkText = null;
                try
                {
                    using var doc = JsonDocument.Parse(data);
                    if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                    {
                        var delta = choices[0].GetProperty("delta");
                        if (delta.TryGetProperty("content", out var contentProp))
                        {
                            chunkText = contentProp.GetString();
                        }
                    }
                }
                catch
                {
                    // ข้ามบรรทัด JSON ที่ผิดรูปแบบ
                }

                if (!string.IsNullOrEmpty(chunkText))
                {
                    yield return chunkText;
                }
            }
        }
    }

    private async Task<HttpResponseMessage> SendChatRequestAsync(
        HttpRequestMessage request,
        string url,
        string json,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"{FormatConnectionError(ex)}\n\n[Debug Info]\nEndpoint: {url}\nPayload:\n{json}", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new Exception($"การเชื่อมต่อไปยัง AI หมดเวลา (Timeout)\n\n[Debug Info]\nEndpoint: {url}\nPayload:\n{json}", ex);
        }
    }

    public async Task<List<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        string url = GetModelsUrl();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyHeaders(request);

        using HttpResponseMessage response = await SendModelsRequestAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"ไม่สามารถดึงรายชื่อโมเดลได้ (HTTP {(int)response.StatusCode})");
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        var models = new List<ModelInfo>();
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in dataProp.EnumerateArray())
            {
                if (item.TryGetProperty("id", out var idProp))
                {
                    string id = idProp.GetString() ?? "";
                    models.Add(new ModelInfo { Id = id, Name = id });
                }
            }
        }

        return models;
    }

    private async Task<HttpResponseMessage> SendModelsRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new Exception(FormatConnectionError(ex), ex);
        }
    }

    public async Task<(bool Success, string Message)> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var models = await GetModelsAsync(cancellationToken);
            return (true, $"เชื่อมต่อสำเร็จ! พบโมเดลทั้งหมด {models.Count} ตัว");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private string FormatConnectionError(HttpRequestException ex)
    {
        if (_settings.Backend == BackendType.Ollama && _settings.BaseUrl.Contains("localhost:11434"))
        {
            return "ไม่พบ Ollama ที่ localhost:11434 กรุณาเปิดโปรแกรม Ollama ก่อนใช้งาน";
        }
        if (_settings.Backend == BackendType.LmStudio && _settings.BaseUrl.Contains("localhost:1234"))
        {
            return "ไม่พบ LM Studio ที่ localhost:1234 กรุณาเปิด Local Server ในโปรแกรม LM Studio ก่อนใช้งาน";
        }
        return $"ไม่สามารถเชื่อมต่อไปยัง AI Backend ที่ {_settings.BaseUrl} ได้: {ex.Message}";
    }
}
