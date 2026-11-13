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
            _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) }; // เผื่อโมเดลขนาดใหญ่ประมวลผลนาน
        }
        else
        {
            _httpClient = httpClient;
        }
    }

    private string GetCompletionsUrl()
    {
        string baseUrl = _settings.BaseUrl.TrimEnd('/');
        if (baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            return $"{baseUrl}/chat/completions";
        }
        return $"{baseUrl}/v1/chat/completions";
    }

    private string GetModelsUrl()
    {
        string baseUrl = _settings.BaseUrl.TrimEnd('/');
        if (baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            return $"{baseUrl}/models";
        }
        return $"{baseUrl}/v1/models";
    }

    private void ApplyHeaders(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        }
    }

    public async IAsyncEnumerable<string> StreamChatAsync(
        IEnumerable<ChatMessage> messages,
        float temperature,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string url = GetCompletionsUrl();
        var requestBody = new
        {
            model = _settings.ModelName,
            messages,
            temperature,
            stream = true
        };

        string json = JsonSerializer.Serialize(requestBody);
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        ApplyHeaders(request);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"{FormatConnectionError(ex)}\n\n[Debug Info]\nEndpoint: {url}\nPayload:\n{json}", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new Exception($"การเชื่อมต่อไปยัง AI หมดเวลา (Timeout)\n\n[Debug Info]\nEndpoint: {url}\nPayload:\n{json}", ex);
        }

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

    public async Task<List<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        string url = GetModelsUrl();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyHeaders(request);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new Exception(FormatConnectionError(ex), ex);
        }

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

    public async Task PullOllamaModelAsync(
        string modelName,
        IProgress<string> progress,
        CancellationToken cancellationToken = default)
    {
        // คำนวณ Ollama base URL จาก settings (ตัด /v1 ออก)
        string baseUrl = _settings.BaseUrl.TrimEnd('/');
        if (baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            baseUrl = baseUrl.Substring(0, baseUrl.Length - 3);
        }
        string pullUrl = $"{baseUrl}/api/pull";

        var body = new { name = modelName, stream = true };
        string json = JsonSerializer.Serialize(body);

        using var request = new HttpRequestMessage(HttpMethod.Post, pullUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string err = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"ไม่สามารถดาวน์โหลดโมเดลได้: {err}");
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? line = await reader.ReadLineAsync(cancellationToken);
            if (line == null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                string status = doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
                if (doc.RootElement.TryGetProperty("completed", out var comp) &&
                    doc.RootElement.TryGetProperty("total", out var tot))
                {
                    long completedBytes = comp.GetInt64();
                    long totalBytes = tot.GetInt64();
                    double pct = totalBytes > 0 ? (double)completedBytes / totalBytes * 100 : 0;
                    progress.Report($"{status} ({pct:F1}%)");
                }
                else
                {
                    progress.Report(status);
                }
            }
            catch
            {
                // ข้ามข้อมูลที่ไม่ใช่ JSON
            }
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

    public static async Task<(BackendType? DetectedBackend, string BaseUrl, string? FirstModelId)> ProbeLocalBackendsAsync(
        HttpClient? httpClient = null,
        CancellationToken cancellationToken = default)
    {
        bool ownsClient = httpClient == null;
        var client = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        try
        {
            var candidates = new[]
            {
                (Backend: BackendType.Ollama, BaseUrl: "http://localhost:11434/v1"),
                (Backend: BackendType.LmStudio, BaseUrl: "http://localhost:1234/v1")
            };

            foreach (var candidate in candidates)
            {
                try
                {
                    var probeSettings = new AppSettings
                    {
                        Backend = candidate.Backend,
                        BaseUrl = candidate.BaseUrl
                    };
                    var probeClient = new LlmClient(probeSettings, client);
                    var models = await probeClient.GetModelsAsync(cancellationToken);
                    return (candidate.Backend, candidate.BaseUrl, models.Count > 0 ? models[0].Id : null);
                }
                catch
                {
                    // ลอง backend ถัดไปเมื่อเชื่อมต่อไม่ได้หรือ timeout
                }
            }

            return (null, string.Empty, null);
        }
        finally
        {
            if (ownsClient)
            {
                client.Dispose();
            }
        }
    }
}
