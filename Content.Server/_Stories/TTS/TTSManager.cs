using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Content.Shared._Stories.SCCVars;
using Prometheus;
using Robust.Shared.Configuration;

namespace Content.Server._Stories.TTS;

public sealed class TTSManager
{

    private static readonly Histogram RequestTimings = Metrics.CreateHistogram(
        "tts_req_timings",
        "Timings of TTS API requests",
        new HistogramConfiguration()
        {
            LabelNames = new[] { "type" },
            Buckets = Histogram.ExponentialBuckets(.1, 1.5, 10),
        });

    private static readonly Counter WantedCount = Metrics.CreateCounter(
        "tts_wanted_count",
        "Amount of wanted TTS audio.");

    private static readonly Counter ReusedCount = Metrics.CreateCounter(
        "tts_reused_count",
        "Amount of reused TTS audio from cache.");

    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private readonly HttpClient _httpClient = new();

    private ISawmill _sawmill = default!;

    private readonly ConcurrentDictionary<string, byte[]> _cache = new();
    private readonly List<string> _cacheKeysSeq = new();

    private readonly ConcurrentDictionary<string, Task<byte[]?>> _pendingRequests = new();

    private int _maxCachedCount = 200;
    private string _apiUrl = string.Empty;
    private string _apiToken = string.Empty;

    public void Initialize()
    {
        _sawmill = Logger.GetSawmill("tts");
        _cfg.OnValueChanged(SCCVars.TTSMaxCache, val =>
        {
            _maxCachedCount = val;
            ResetCache();
        }, true);
        _cfg.OnValueChanged(SCCVars.TTSApiUrl, v => _apiUrl = v, true);
        _cfg.OnValueChanged(SCCVars.TTSApiToken, v =>
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", v);
            _apiToken = v;
        },
        true);
    }

    public Task<byte[]?> ConvertTextToSpeech(string speaker, string text)
    {
        WantedCount.Inc();
        var cacheKey = GenerateCacheKey(speaker, text);

        if (_cache.TryGetValue(cacheKey, out var data))
        {
            ReusedCount.Inc();
            _sawmill.Debug($"Use cached sound for '{text}' speech by '{speaker}' speaker");
            return Task.FromResult<byte[]?>(data);
        }

        return _pendingRequests.GetOrAdd(cacheKey, (key) => GenerateAndCacheAudio(speaker, text, key));
    }

    private async Task<byte[]?> GenerateAndCacheAudio(string speaker, string text, string cacheKey)
    {
        _sawmill.Debug($"Generate new audio for '{text}' speech by '{speaker}' speaker");

        var reqTime = DateTime.UtcNow;
        try
        {
            var timeout = _cfg.GetCVar(SCCVars.TTSApiTimeout);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));

            var requestUrl = $"{_apiUrl}" + ToQueryString(new NameValueCollection() {
                { "speaker", speaker },
                { "text", text },
                { "ext", "ogg" }});

            var response = await _httpClient.GetAsync(requestUrl, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    _sawmill.Warning("TTS request was rate limited");
                    return null;
                }

                _sawmill.Error($"TTS request returned bad status code: {response.StatusCode}");
                return null;
            }

            var soundData = await response.Content.ReadAsByteArrayAsync();

            _cache.TryAdd(cacheKey, soundData);
            lock (_cacheKeysSeq)
            {
                _cacheKeysSeq.Add(cacheKey);
                if (_cacheKeysSeq.Count > _maxCachedCount)
                {
                    var firstKey = _cacheKeysSeq.First();
                    _cache.TryRemove(firstKey, out _);
                    _cacheKeysSeq.Remove(firstKey);
                }
            }


            _sawmill.Debug($"Generated new audio for '{text}' speech by '{speaker}' speaker ({soundData.Length} bytes)");
            RequestTimings.WithLabels("Success").Observe((DateTime.UtcNow - reqTime).TotalSeconds);

            return soundData;
        }
        catch (TaskCanceledException)
        {
            RequestTimings.WithLabels("Timeout").Observe((DateTime.UtcNow - reqTime).TotalSeconds);
            _sawmill.Error($"Timeout of request generation new audio for '{text}' speech by '{speaker}' speaker");
            return null;
        }
        catch (Exception e)
        {
            RequestTimings.WithLabels("Error").Observe((DateTime.UtcNow - reqTime).TotalSeconds);
            _sawmill.Error($"Failed of request generation new sound for '{text}' speech by '{speaker}' speaker\n{e}");
            return null;
        }
        finally
        {
            _pendingRequests.TryRemove(cacheKey, out _);
        }
    }


    private static string ToQueryString(NameValueCollection nvc)
    {
        var array = (
            from key in nvc.AllKeys
            from value in nvc.GetValues(key) ?? Array.Empty<string>()
            select $"{key}={HttpUtility.UrlEncode(value)}"
            ).ToArray();

        return "?" + string.Join("&", array);
    }

    public void ResetCache()
    {
        _cache.Clear();
        lock (_cacheKeysSeq)
        {
            _cacheKeysSeq.Clear();
        }
    }

    private string GenerateCacheKey(string speaker, string text)
    {
        var key = $"{speaker}/{text}";
        var keyData = Encoding.UTF8.GetBytes(key);
        var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(keyData);
        return Convert.ToHexString(bytes);
    }

    private struct GenerateVoiceRequest
    {
        public GenerateVoiceRequest()
        {
        }
        [JsonPropertyName("speaker")]
        public string Speaker { get; set; } = "";

        [JsonPropertyName("text")]
        public string Text { get; set; } = "";

        [JsonPropertyName("ext")]
        public string Extension { get; } = "ogg";
    }

    private struct GenerateVoiceResponse
    {
        [JsonPropertyName("results")]
        public List<VoiceResult> Results { get; set; }

        [JsonPropertyName("original_sha1")]
        public string Hash { get; set; }
    }

    private struct VoiceResult
    {
        [JsonPropertyName("audio")]
        public string Audio { get; set; }
    }
}
