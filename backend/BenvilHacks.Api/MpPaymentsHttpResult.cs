namespace BenvilHacks.Api;

/// <summary>Resultado de uma chamada HTTP ao POST /v1/payments (ou corpo parseado quando 2xx).</summary>
public sealed record MpPaymentsHttpResult(
    MpPaymentApiResult? ParsedResult,
    int HttpStatusCode,
    string ResponseBodyRaw,
    string RequestHeadersLog,
    string ResponseHeadersLog,
    string RequestContentTypeLog);
