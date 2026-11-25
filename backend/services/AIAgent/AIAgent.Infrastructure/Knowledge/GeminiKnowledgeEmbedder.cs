using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIAgent.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIAgent.Infrastructure.Knowledge;

public sealed class GeminiKnowledgeEmbedder : IKnowledgeEmbedder
{
    public const string DefaultModel = "gemini-embedding-001";
    private const int BatchSize = 50;
    private static readonly TimeSpan Pause = TimeSpan.FromSeconds(60);

    private readonly HttpClient _http;
    private readonly string? _key;
    private readonly ILogger<GeminiKnowledgeEmbedder> _logger;
    private long _pausedUntilTicks;

    public GeminiKnowledgeEmbedder(HttpClient http, IConfiguration configuration, ILogger<GeminiKnowledgeEmbedder> logger)
    {
        _http = http;
        _logger = logger;
        _key = configuration["KnowledgeBase:EmbeddingApiKey"] ?? configuration["AI:ApiKey"] ?? configuration["GEMINI_API_KEY"];
        Model = configuration["KnowledgeBase:EmbeddingModel"] ?? DefaultModel;
        Dimensions = int.TryParse(configuration["KnowledgeBase:EmbeddingDimensions"], out var d) && d is >= 2 and <= 3072 ? d : 768;
        if (_http.Timeout > TimeSpan.FromSeconds(20)) _http.Timeout = TimeSpan.FromSeconds(20);
    }

    public bool Enabled => !string.IsNullOrWhiteSpace(_key);
    public string Model { get; }
    public int Dimensions { get; }

    private bool Paused => DateTime.UtcNow.Ticks < Interlocked.Read(ref _pausedUntilTicks);

    public async Task<float[]?> EmbedQueryAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var result = await EmbedAsync([text], "RETRIEVAL_QUERY", cancellationToken);
        return result[0];
    }

    public async Task<IReadOnlyList<float[]?>> EmbedPassagesAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        var all = new List<float[]?>(texts.Count);
        for (var i = 0; i < texts.Count; i += BatchSize)
        {
            var batch = texts.Skip(i).Take(BatchSize).ToArray();
            all.AddRange(await EmbedAsync(batch, "RETRIEVAL_DOCUMENT", cancellationToken));
        }
        return all;
    }

    private async Task<float[]?[]> EmbedAsync(string[] texts, string taskType, CancellationToken ct)
    {
        var none = new float[]?[texts.Length];
        if (!Enabled || Paused) return none;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:batchEmbedContents")
            {
                Content = JsonContent.Create(new
                {
                    requests = texts.Select(t => new
                    {
                        model = $"models/{Model}",
                        content = new { parts = new[] { new { text = Trim(t) } } },
                        taskType,
                        outputDimensionality = Dimensions
                    })
                })
            };
            request.Headers.Add("x-goog-api-key", _key);
            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                Interlocked.Exchange(ref _pausedUntilTicks, (DateTime.UtcNow + Pause).Ticks);
                _logger.LogWarning("Embedding service answered {Status}; company document search uses keywords only for a minute.", (int)response.StatusCode);
                return none;
            }

            var body = await response.Content.ReadFromJsonAsync<EmbedResponse>(cancellationToken: ct);
            var vectors = body?.Embeddings;
            if (vectors is null || vectors.Count != texts.Length) return none;
            for (var i = 0; i < vectors.Count; i++)
            {
                var values = vectors[i].Values;
                if (values is { Length: > 0 } && values.Length == Dimensions) none[i] = Normalize(values);
            }
            return none;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            if (ct.IsCancellationRequested) throw;
            Interlocked.Exchange(ref _pausedUntilTicks, (DateTime.UtcNow + Pause).Ticks);
            _logger.LogWarning("Embedding service could not be reached ({Kind}); company document search uses keywords only for a minute.", ex.GetType().Name);
            return none;
        }
    }

    
    private static float[] Normalize(float[] v)
    {
        var sum = 0.0;
        foreach (var x in v) sum += (double)x * x;
        var norm = Math.Sqrt(sum);
        if (norm < 1e-9) return v;
        var result = new float[v.Length];
        for (var i = 0; i < v.Length; i++) result[i] = (float)(v[i] / norm);
        return result;
    }

    private static string Trim(string text) => text.Length > 8000 ? text[..8000] : text;

    private sealed class EmbedResponse
    {
        [JsonPropertyName("embeddings")] public List<EmbeddingValues>? Embeddings { get; set; }
    }

    private sealed class EmbeddingValues
    {
        [JsonPropertyName("values")] public float[]? Values { get; set; }
    }
}
