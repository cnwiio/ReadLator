using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace NovelTranslator.Core.Llm;

public class ChatMessage
{
    public ChatMessage() { }

    public ChatMessage(string role, string content)
    {
        Role = role;
        Content = content;
    }

    [JsonPropertyName("role")]
    public string Role { get; set; } = "user"; // "system", "user", "assistant"

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public class ModelInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public interface ILlmClient
{
    IAsyncEnumerable<string> StreamChatAsync(
        IEnumerable<ChatMessage> messages,
        float temperature,
        CancellationToken cancellationToken = default);

    Task<List<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default);

    Task<(bool Success, string Message)> TestConnectionAsync(CancellationToken cancellationToken = default);

    Task PullOllamaModelAsync(
        string modelName,
        IProgress<string> progress,
        CancellationToken cancellationToken = default);
}
