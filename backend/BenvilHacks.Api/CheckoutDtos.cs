using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BenvilHacks.Api;

public sealed class CreateCheckoutRequest
{
    [Required, MaxLength(64)]
    public string GameId { get; set; } = "";

    [Required, MaxLength(128)]
    public string PlanId { get; set; } = "";

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = "";

    /// <summary>Telefone com DDD, apenas dígitos ou formato livre curto.</summary>
    [Required, MinLength(8), MaxLength(32)]
    public string Phone { get; set; } = "";

    /// <summary>CPF do comprador (com ou sem máscara; a API normaliza para dígitos).</summary>
    [Required, MinLength(11), MaxLength(18)]
    public string Cpf { get; set; } = "";
}

public sealed class CreateCheckoutResponse
{
    public string CheckoutUrl { get; set; } = "";
    public string OrderId { get; set; } = "";
    public string PreferenceId { get; set; } = "";
}

public sealed class VerifyPaymentRequest
{
    [Required]
    public string OrderId { get; set; } = "";

    [Required]
    public string PaymentId { get; set; } = "";
}

public sealed class VerifyPaymentResponse
{
    public bool Ok { get; set; }
    public string? LicenseKey { get; set; }
    public string? Message { get; set; }
    public string? Email { get; set; }
}

// Mercado Pago API shapes
public sealed class MpItem
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; } = 1;

    [JsonPropertyName("unit_price")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("currency_id")]
    public string CurrencyId { get; set; } = "BRL";
}

public sealed class MpPayer
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = "";
}

public sealed class MpBackUrls
{
    [JsonPropertyName("success")]
    public string Success { get; set; } = "";

    [JsonPropertyName("failure")]
    public string Failure { get; set; } = "";

    [JsonPropertyName("pending")]
    public string Pending { get; set; } = "";
}

public sealed class MpCreatePreferenceRequest
{
    [JsonPropertyName("items")]
    public List<MpItem> Items { get; set; } = [];

    [JsonPropertyName("payer")]
    public MpPayer? Payer { get; set; }

    [JsonPropertyName("external_reference")]
    public string ExternalReference { get; set; } = "";

    [JsonPropertyName("back_urls")]
    public MpBackUrls BackUrls { get; set; } = new();

    /// <summary>Só use com back_urls em HTTPS; com http://localhost o MP rejeita (invalid_auto_return).</summary>
    [JsonPropertyName("auto_return")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AutoReturn { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}

public sealed class MpPreferenceResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("init_point")]
    public string? InitPoint { get; set; }

    [JsonPropertyName("sandbox_init_point")]
    public string? SandboxInitPoint { get; set; }
}

public sealed class MpPaymentResponse
{
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("external_reference")]
    public string? ExternalReference { get; set; }

    [JsonPropertyName("transaction_amount")]
    public decimal? TransactionAmount { get; set; }
}

public sealed class CheckoutSessionResponse
{
    public string OrderId { get; set; } = "";
    public double Amount { get; set; }
    public string ItemTitle { get; set; } = "";
    public string PublicKey { get; set; } = "";
    /// <summary>Valor de MercadoPago:PixSandboxMode (modo de credenciais no painel MP).</summary>
    public string MercadoPagoPixSandboxMode { get; set; } = "";
    /// <summary>Dica curta de credenciais para o checkout.</summary>
    public List<string>? MercadoPagoPixEnvironmentHints { get; set; }
    /// <summary>production_app_usr | test | missing | unknown — confirma tipo de Access Token carregado na API.</summary>
    public string MercadoPagoAccessTokenKind { get; set; } = "";
    /// <summary>Regra de sufixo do e-mail comprador de teste (se configurada).</summary>
    public string? MercadoPagoPixPayerEmailMustEndWith { get; set; }
}

/// <summary>Resposta do POST /v1/payments (cartão, PIX, etc.).</summary>
public sealed class MpPaymentApiResult
{
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("status_detail")]
    public string? StatusDetail { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("point_of_interaction")]
    public MpPointOfInteraction? PointOfInteraction { get; set; }
}

public sealed class MpPointOfInteraction
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("transaction_data")]
    public MpPixTransactionData? TransactionData { get; set; }
}

public sealed class MpPixTransactionData
{
    [JsonPropertyName("qr_code")]
    public string? QrCode { get; set; }

    [JsonPropertyName("qr_code_base64")]
    public string? QrCodeBase64 { get; set; }

    [JsonPropertyName("ticket_url")]
    public string? TicketUrl { get; set; }
}

public sealed class CreatePixPaymentRequest
{
    [Required]
    public string OrderId { get; set; } = "";

    [JsonPropertyName("transaction_amount")]
    public double? TransactionAmount { get; set; }
}

/// <summary>Uma tentativa HTTP ao Mercado Pago (request/response completos para diagnóstico).</summary>
public sealed class MpHttpAttemptLog
{
    public int Attempt { get; set; }
    public string PayerEmailUsed { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string RequestJson { get; set; } = "";
    public string RequestHeaders { get; set; } = "";
    public string RequestContentType { get; set; } = "";
    public int HttpStatus { get; set; }
    public string ResponseHeaders { get; set; } = "";
    public string ResponseBody { get; set; } = "";
}

public sealed class CreatePixPaymentResponse
{
    public bool Ok { get; set; }
    public bool AwaitingPixTransfer { get; set; }
    public string? Message { get; set; }
    /// <summary>Detalhe devolvido pelo Mercado Pago (erros de validação, etc.).</summary>
    public string? Detail { get; set; }
    /// <summary>Status HTTP da chamada ao Mercado Pago (ex.: 400, 201).</summary>
    public int? MpHttpStatus { get; set; }
    /// <summary>JSON exatamente como enviado no POST /v1/payments (diagnóstico).</summary>
    public string? MpRequestJson { get; set; }
    /// <summary>Corpo bruto da resposta HTTP do Mercado Pago (diagnóstico).</summary>
    public string? MpResponseBody { get; set; }
    /// <summary>Todas as tentativas (retry + e-mail alternativo), quando houve falha ou só erros HTTP.</summary>
    public List<MpHttpAttemptLog>? MpAttempts { get; set; }
    /// <summary>Orientação de sandbox/credenciais (sem alterar o payload enviado ao MP).</summary>
    public List<string>? MpEnvironmentHints { get; set; }
    public long? PaymentId { get; set; }
    public string? QrCode { get; set; }
    public string? QrCodeBase64 { get; set; }
    public string? TicketUrl { get; set; }
    public string? LicenseKey { get; set; }
    public string? Email { get; set; }
}
