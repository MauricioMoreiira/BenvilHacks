using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace BenvilHacks.Api;

public sealed class MercadoPagoClient(HttpClient http, IOptions<MercadoPagoOptions> options, ILogger<MercadoPagoClient> log)
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public const string PaymentsEndpoint = "https://api.mercadopago.com/v1/payments";

    private readonly MercadoPagoOptions _opt = options.Value;

    internal static string AccessTokenPresenceSummary(string? token)
    {
        var t = (token ?? "").Trim();
        if (t.Length == 0)
            return "ausente";
        if (t.StartsWith("TEST-", StringComparison.OrdinalIgnoreCase))
            return "presente (TEST-…)";
        if (t.StartsWith("APP_USR-", StringComparison.OrdinalIgnoreCase))
            return "presente (APP_USR-…)";
        return "presente";
    }

    /// <summary>Remove token de cartão do JSON antes de escrever em log (PIX não tem token).</summary>
    private static string RedactCardTokenForLog(string jsonBody) =>
        Regex.Replace(jsonBody, @"""token""\s*:\s*""[^""]*""", """token"":""[REDACTADO]""", RegexOptions.IgnoreCase);

    private static string FormatRequestHeaders(HttpRequestMessage req)
    {
        var sb = new StringBuilder();
        foreach (var kv in req.Headers)
        {
            if (string.Equals(kv.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine("Authorization: Bearer [REDACTADO]");
                continue;
            }

            sb.Append(kv.Key).Append(": ").AppendLine(string.Join(", ", kv.Value ?? []));
        }

        if (req.Content?.Headers is { } ch)
        {
            foreach (var kv in ch)
                sb.Append(kv.Key).Append(": ").AppendLine(string.Join(", ", kv.Value ?? []));
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatResponseHeaders(HttpResponseMessage res)
    {
        var sb = new StringBuilder();
        foreach (var kv in res.Headers)
            sb.Append(kv.Key).Append(": ").AppendLine(string.Join(", ", kv.Value ?? []));
        foreach (var kv in res.Content.Headers)
            sb.Append(kv.Key).Append(": ").AppendLine(string.Join(", ", kv.Value ?? []));
        return sb.ToString().TrimEnd();
    }

    private static string ContentTypeSummary(HttpContent? c)
    {
        if (c?.Headers.ContentType is null)
            return "(sem Content-Type no content)";
        var ct = c.Headers.ContentType;
        return $"{ct.MediaType}; charset={ct.CharSet ?? "n/a"}";
    }

    public async Task<(MpPreferenceResponse? Pref, string? ErrorBody)> CreatePreferenceAsync(
        MpCreatePreferenceRequest body,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opt.AccessToken))
        {
            log.LogWarning("MercadoPago AccessToken não configurado.");
            return (null, "AccessToken vazio. Use o perfil http no launchSettings ou MercadoPago__AccessToken.");
        }

        var json = JsonSerializer.Serialize(body, JsonOpts);
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.mercadopago.com/checkout/preferences")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opt.AccessToken.Trim());

        using var res = await http.SendAsync(req, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            var clip = raw.Length > 400 ? raw[..400] + "…" : raw;
            log.LogWarning("Mercado Pago preferences falhou: HTTP {Status} {Body}", (int)res.StatusCode, clip);
            return (null, raw);
        }

        var pref = JsonSerializer.Deserialize<MpPreferenceResponse>(raw, JsonOpts);
        return (pref, null);
    }

    public async Task<MpPaymentResponse?> GetPaymentAsync(long paymentId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opt.AccessToken))
            return null;

        using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.mercadopago.com/v1/payments/{paymentId}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opt.AccessToken.Trim());

        using var res = await http.SendAsync(req, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            var clip = raw.Length > 400 ? raw[..400] + "…" : raw;
            log.LogWarning("Mercado Pago payment GET falhou: HTTP {Status} {Body}", (int)res.StatusCode, clip);
            return null;
        }

        var p = JsonSerializer.Deserialize<MpPaymentResponse>(raw, JsonOpts);
        return p;
    }

    /// <summary>Cria cobrança via POST /v1/payments. Content-Type: application/json; charset=utf-8. Authorization: Bearer.</summary>
    public async Task<MpPaymentsHttpResult> PostPaymentJsonAsync(string jsonBody, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opt.AccessToken))
        {
            const string emptyTok = """{"message":"AccessToken vazio na API BenvilHacks. Configure MercadoPago:AccessToken."}""";
            log.LogWarning("MercadoPago AccessToken não configurado ao chamar POST /v1/payments.");
            return new MpPaymentsHttpResult(null, 401, emptyTok, "", "", "");
        }

        var token = _opt.AccessToken.Trim();

        using var req = new HttpRequestMessage(HttpMethod.Post, PaymentsEndpoint)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json"),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var idem = Guid.NewGuid().ToString("D");
        req.Headers.TryAddWithoutValidation("X-Idempotency-Key", idem);

        var reqHeadersForLog = FormatRequestHeaders(req);
        var contentTypeLog = ContentTypeSummary(req.Content);
        var logRequest = RedactCardTokenForLog(jsonBody);

        log.LogDebug(
            "Mercado Pago POST /v1/payments | headers:\n{ReqHeaders}\nbody:\n{Body}",
            reqHeadersForLog,
            logRequest);

        using var res = await http.SendAsync(req, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        var code = (int)res.StatusCode;
        var resHeadersForLog = FormatResponseHeaders(res);

        log.LogDebug("Mercado Pago POST /v1/payments → HTTP {Status}", code);

        if (!res.IsSuccessStatusCode)
        {
            log.LogWarning(
                "Mercado Pago POST /v1/payments falhou | HttpStatus={Status} | AccessToken={TokSummary} | Body={Body}",
                code,
                AccessTokenPresenceSummary(token),
                raw.Length > 500 ? raw[..500] + "…" : raw);
            return new MpPaymentsHttpResult(null, code, raw, reqHeadersForLog, resHeadersForLog, contentTypeLog);
        }

        var parsed = JsonSerializer.Deserialize<MpPaymentApiResult>(raw, JsonOpts);
        return new MpPaymentsHttpResult(parsed, code, raw, reqHeadersForLog, resHeadersForLog, contentTypeLog);
    }
}
