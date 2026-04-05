namespace BenvilHacks.Api;

public sealed class MercadoPagoOptions
{
    public const string SectionName = "MercadoPago";

    public string AccessToken { get; set; } = "";
    public string PublicKey { get; set; } = "";

    /// <summary>Chamadas ao POST /v1/payments em caso de HTTP 500 (mínimo 1). Ex.: 3 = tentativa inicial + 2 retries.</summary>
    public int PixHttp500MaxAttempts { get; set; } = 3;

    /// <summary>E-mail alternativo do payer na última tentativa após 500 (sandbox MP costuma usar test_user@testuser.com).</summary>
    public string? PixAlternatePayerEmail { get; set; }

    /// <summary>Milissegundos antes de cada retry após 500 no PIX.</summary>
    public int PixHttp500RetryDelayMs { get; set; } = 500;

    /// <summary>
    /// test_keys_real_account: TEST- da sua conta real (doc Bricks — cartão/offline).
    /// production_keys_test_seller: APP_USR/PK na conta vendedora de teste (Contas de teste).
    /// production_real_account: APP_USR/PK de produção da conta real (cobranças reais, ex. PIX).
    /// </summary>
    public string PixSandboxMode { get; set; } = MercadoPagoPixSandboxGuidance.ModeProductionRealAccount;

    /// <summary>Se vazio, descrição do PIX = título do plano; senão valor fixo (ex.: Produto).</summary>
    public string? PixPaymentDescription { get; set; }

    /// <summary>Incluir external_reference no JSON do PIX (recomendado para conciliar pedidos).</summary>
    public bool PixIncludeExternalReference { get; set; } = true;

    /// <summary>Rejeitar credenciais que comecem com TEST- (obrigatório em modos de produção com APP_USR).</summary>
    public bool RejectTestMercadoPagoCredentials { get; set; }

    /// <summary>E-mails que não podem ser payer no PIX (ex.: conta real), separados por ; ou ,</summary>
    public string? PixPayerEmailBlocklist { get; set; }

    /// <summary>Se preenchido, o e-mail do payer deve terminar assim (ex.: @testuser.com).</summary>
    public string? PixPayerEmailMustEndWith { get; set; }
}

public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    /// <summary>URL base do site (ex.: https://seudominio.com ou http://localhost:5173)</summary>
    public string BaseUrl { get; set; } = "http://localhost:5173";
}
