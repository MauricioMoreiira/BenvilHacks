using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using BenvilHacks.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.MercadoPago.local.json", optional: true, reloadOnChange: true);

builder.Services.AddSingleton<PendingOrderStore>();
builder.Services.Configure<MercadoPagoOptions>(builder.Configuration.GetSection(MercadoPagoOptions.SectionName));
builder.Services.Configure<FrontendOptions>(builder.Configuration.GetSection(FrontendOptions.SectionName));
builder.Services.AddHttpClient<MercadoPagoClient>();

builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p =>
    {
        var origins = new List<string>
        {
            "http://localhost:5173",
            "http://127.0.0.1:5173",
            "http://localhost:5174",
            "http://127.0.0.1:5174",
            "http://localhost:4173",
            "http://127.0.0.1:4173",
        };
        var extra = builder.Configuration["Cors:Origins"];
        if (!string.IsNullOrWhiteSpace(extra))
        {
            origins.AddRange(extra.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        p.WithOrigins(origins.Distinct(StringComparer.OrdinalIgnoreCase).ToArray())
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

{
    var mpTok = app.Configuration["MercadoPago:AccessToken"]?.Trim() ?? "";
    if (app.Environment.IsProduction() && mpTok.StartsWith("TEST-", StringComparison.OrdinalIgnoreCase))
    {
        app.Logger.LogCritical(
            "Mercado Pago: ambiente Production com AccessToken de TESTE (TEST-). Use APP_USR ou não publique assim.");
    }

    var pixMode = app.Configuration["MercadoPago:PixSandboxMode"] ?? MercadoPagoPixSandboxGuidance.ModeProductionRealAccount;
    if (!string.IsNullOrEmpty(mpTok) &&
        !MercadoPagoPixSandboxGuidance.TokenMatchesConfiguredMode(mpTok, pixMode))
    {
        app.Logger.LogCritical(
            "Mercado Pago: PixSandboxMode={Mode} incompatível com o formato do AccessToken.",
            MercadoPagoPixSandboxGuidance.NormalizeMode(pixMode));
    }
}

app.UseCors();

static string NewLicenseKey()
{
    Span<byte> b = stackalloc byte[10];
    RandomNumberGenerator.Fill(b);
    return $"BVL-{Convert.ToHexString(b)}";

}

static string PickCheckoutUrl(MpPreferenceResponse? pref, string accessToken)
{
    if (pref is null) return "";
    var test = accessToken.Trim().StartsWith("TEST-", StringComparison.OrdinalIgnoreCase);
    if (test && !string.IsNullOrWhiteSpace(pref.SandboxInitPoint))
        return pref.SandboxInitPoint!;
    return pref.InitPoint ?? pref.SandboxInitPoint ?? "";
}

static List<string> BuildPixFailureEnvironmentHints(MercadoPagoOptions o, int lastHttpStatus, string? mpResponseBody)
{
    var list = new List<string>();
    var tok = o.AccessToken ?? "";
    list.AddRange(MercadoPagoPixSandboxGuidance.ExtraHintsOnPixHttpFailure(o.PixSandboxMode, tok, lastHttpStatus));

    if (lastHttpStatus == 401 &&
        mpResponseBody != null &&
        mpResponseBody.Contains("Unauthorized use of live credentials", StringComparison.OrdinalIgnoreCase))
    {
        list.Add(
            "HTTP 401: Mercado Pago rejeitou o Access Token nesta operação. Confira no painel se Public Key e Access Token são da mesma aplicação e se o produto de pagamentos via API está habilitado.");
    }

    if (lastHttpStatus == 401 &&
        mpResponseBody != null &&
        mpResponseBody.Contains("Must provide your access_token", StringComparison.OrdinalIgnoreCase))
    {
        list.Add(
            "HTTP 401: Public Key e Access Token podem estar trocados no appsettings.MercadoPago.local.json (token = credencial longa; Public Key = APP_USR + UUID). Reinicie a API.");
    }

    return list;
}

static string MpRedactAuthHeaders(string? raw)
{
    if (string.IsNullOrEmpty(raw))
        return raw ?? "";
    return Regex.Replace(raw, @"(?im)^Authorization:\s*Bearer\s+\S+", "Authorization: Bearer [REDACTADO]");
}

app.MapGet("/api/health", () => Results.Ok(new { ok = true, service = "benvil-api" }));

/// <summary>Inicia checkout PIX: grava pedido e devolve chave pública + valor.</summary>
app.MapPost("/api/checkout/session", (
    CreateCheckoutRequest body,
    PendingOrderStore orders,
    IOptions<MercadoPagoOptions> mpOpt) =>
{
    if (!PlanCatalog.TryGet(body.PlanId, out var plan))
        return Results.BadRequest(new { error = "Plano inválido ou desatualizado. Atualize a página e tente de novo." });

    var mpCfg = mpOpt.Value;
    var pk = mpCfg.PublicKey?.Trim();
    var at = mpCfg.AccessToken?.Trim() ?? "";
    if (string.IsNullOrWhiteSpace(pk) || string.IsNullOrWhiteSpace(at))
    {
        return Results.BadRequest(new
        {
            error = "Mercado Pago: faltam Public Key e/ou Access Token na API.",
            details =
                "1) Preencha MercadoPago:PublicKey e MercadoPago:AccessToken em appsettings.json ou appsettings.MercadoPago.local.json (pasta da API), com o par da mesma aplicação no painel (Suas integrações). " +
                "2) Ou defina variáveis de ambiente MercadoPago__PublicKey e MercadoPago__AccessToken. Reinicie a API após salvar.",
            missingPublicKey = string.IsNullOrWhiteSpace(pk),
            missingAccessToken = string.IsNullOrWhiteSpace(at),
        });
    }

    if (MercadoPagoCredentialValidation.LooksLikeAppUsrPublicKey(at))
    {
        return Results.BadRequest(new
        {
            error =
                "Mercado Pago: o valor em MercadoPago:AccessToken parece ser uma Public Key (formato curto APP_USR + UUID). No painel use o Access Token (credencial longa), não a Public Key. Confira se Public Key e Access Token não estão trocados no appsettings.MercadoPago.local.json.",
        });
    }

    if (MercadoPagoCredentialValidation.LooksLikeAppUsrAccessToken(pk) &&
        !MercadoPagoCredentialValidation.LooksLikeAppUsrPublicKey(pk))
    {
        return Results.BadRequest(new
        {
            error =
                "Mercado Pago: o valor em MercadoPago:PublicKey parece ser um Access Token (credencial longa). Public Key deve ser a chave curta (APP_USR + UUID). Troque os dois campos no appsettings.MercadoPago.local.json.",
        });
    }

    if (MercadoPagoCredentialValidation.MustRejectTestCredentials(mpCfg))
    {
        if (MercadoPagoCredentialValidation.AccessTokenIsTest(at))
            return Results.BadRequest(new { error = "Access Token não pode ser de teste (TEST-). Use credenciais de produção (APP_USR) da mesma aplicação no painel." });
        if (MercadoPagoCredentialValidation.PublicKeyIsTest(pk))
            return Results.BadRequest(new { error = "Public Key não pode ser de teste (TEST-). Use a Public Key de produção da mesma aplicação no painel." });
    }

    var cpfDigits = new string((body.Cpf ?? "").Where(char.IsDigit).ToArray());
    if (cpfDigits.Length != 11)
        return Results.BadRequest(new { error = "CPF inválido. Informe 11 dígitos." });

    var orderId = Guid.NewGuid().ToString("N");
    var licenseKey = NewLicenseKey();
    var emailStored = CheckoutContactEmailTransform.TransformForStorage(body.Email.Trim());
    var pending = new PendingOrder
    {
        OrderId = orderId,
        LicenseKey = licenseKey,
        Email = emailStored,
        Phone = body.Phone.Trim(),
        Cpf = cpfDigits,
        PlanId = body.PlanId,
        GameId = body.GameId.Trim(),
        ExpectedAmount = plan.Price,
    };

    orders.Put(pending);

    var pixMode = MercadoPagoPixSandboxGuidance.NormalizeMode(mpCfg.PixSandboxMode);
    var hints = MercadoPagoPixSandboxGuidance.HintsForCurrentConfig(pixMode, at);
    var payerSuffix = string.IsNullOrWhiteSpace(mpCfg.PixPayerEmailMustEndWith)
        ? null
        : mpCfg.PixPayerEmailMustEndWith.Trim();

    return Results.Ok(new CheckoutSessionResponse
    {
        OrderId = orderId,
        Amount = (double)decimal.Round(plan.Price, 2, MidpointRounding.AwayFromZero),
        ItemTitle = plan.Title,
        PublicKey = pk,
        MercadoPagoPixSandboxMode = pixMode,
        MercadoPagoPixEnvironmentHints = hints.ToList(),
        MercadoPagoAccessTokenKind = MercadoPagoCredentialValidation.AccessTokenKind(at),
        MercadoPagoPixPayerEmailMustEndWith = payerSuffix,
    });
})
.DisableAntiforgery();

app.MapPost("/api/checkout/create-pix", async (
    CreatePixPaymentRequest body,
    PendingOrderStore orders,
    MercadoPagoClient mp,
    IOptions<MercadoPagoOptions> mpOpt,
    CancellationToken ct) =>
{
    var oid = body.OrderId?.Trim() ?? "";
    if (!orders.TryGet(oid, out PendingOrder? pixOrder) || pixOrder is null)
    {
        return Results.BadRequest(new CreatePixPaymentResponse
        {
            Ok = false,
            Message = "Pedido expirado ou inválido. Volte e inicie o checkout de novo.",
        });
    }

    if (!PlanCatalog.TryGet(pixOrder.PlanId, out var pixPlan))
    {
        return Results.BadRequest(new CreatePixPaymentResponse { Ok = false, Message = "Plano não encontrado." });
    }

    var email = pixOrder.Email.Trim();
    if (string.IsNullOrWhiteSpace(email))
    {
        return Results.BadRequest(new CreatePixPaymentResponse { Ok = false, Message = "E-mail do pedido ausente." });
    }

    var cpfDigits = new string(pixOrder.Cpf.Where(char.IsDigit).ToArray());
    if (cpfDigits.Length != 11)
    {
        return Results.BadRequest(new CreatePixPaymentResponse
        {
            Ok = false,
            Message = "Pedido sem CPF válido. Volte e preencha o checkout de novo.",
        });
    }

    var mpOptions = mpOpt.Value;
    var accessTok = mpOptions.AccessToken?.Trim() ?? "";
    if (string.IsNullOrWhiteSpace(accessTok))
    {
        return Results.BadRequest(new CreatePixPaymentResponse
        {
            Ok = false,
            Message = "MercadoPago:AccessToken não configurado na API.",
        });
    }

    var pkPix = mpOptions.PublicKey?.Trim() ?? "";
    if (MercadoPagoCredentialValidation.LooksLikeAppUsrPublicKey(accessTok))
    {
        return Results.BadRequest(new CreatePixPaymentResponse
        {
            Ok = false,
            Message =
                "Access Token inválido: o valor parece ser uma Public Key (APP_USR + UUID). Use a credencial longa do painel em Access Token e a chave curta em Public Key.",
        });
    }

    if (MercadoPagoCredentialValidation.LooksLikeAppUsrAccessToken(pkPix) &&
        !MercadoPagoCredentialValidation.LooksLikeAppUsrPublicKey(pkPix))
    {
        return Results.BadRequest(new CreatePixPaymentResponse
        {
            Ok = false,
            Message =
                "Public Key inválida: o valor parece ser um Access Token. Troque Public Key (curta) e Access Token (longo) no appsettings.MercadoPago.local.json.",
        });
    }

    if (MercadoPagoCredentialValidation.MustRejectTestCredentials(mpOptions))
    {
        if (MercadoPagoCredentialValidation.AccessTokenIsTest(accessTok))
        {
            return Results.BadRequest(new CreatePixPaymentResponse
            {
                Ok = false,
                Message = "Access Token não pode ser TEST- com esta configuração. Use APP_USR de produção.",
            });
        }

        if (MercadoPagoCredentialValidation.PublicKeyIsTest(mpOptions.PublicKey))
        {
            return Results.BadRequest(new CreatePixPaymentResponse
            {
                Ok = false,
                Message = "Public Key não pode ser TEST-. Use a Public Key de produção da mesma aplicação.",
            });
        }
    }

    if (MercadoPagoCredentialValidation.PayerEmailViolatesRules(email, mpOptions, out var payerErr))
    {
        return Results.BadRequest(new CreatePixPaymentResponse { Ok = false, Message = payerErr });
    }

    var expectedPixAmount = (double)decimal.Round(pixOrder.ExpectedAmount, 2, MidpointRounding.AwayFromZero);
    if (body.TransactionAmount is > 0 and var pixBodyAmt)
    {
        var pixRounded = Math.Round(pixBodyAmt, 2, MidpointRounding.AwayFromZero);
        if (Math.Abs(pixRounded - expectedPixAmount) > 0.05)
        {
            return Results.BadRequest(new CreatePixPaymentResponse
            {
                Ok = false,
                Message = "Valor não confere com o pedido.",
            });
        }
    }

    var pixAmountDecimal = decimal.Round((decimal)expectedPixAmount, 2, MidpointRounding.AwayFromZero);
    var pixDescriptionRaw = string.IsNullOrWhiteSpace(mpOptions.PixPaymentDescription)
        ? $"Benvil Hacks - {pixPlan.Title}"
        : mpOptions.PixPaymentDescription.Trim();
    var pixDescription = MercadoPagoPaymentHelpers.TruncatePaymentDescription(
        MercadoPagoPaymentHelpers.SanitizeDescription(pixDescriptionRaw));

    var maxAttempts = Math.Max(1, mpOptions.PixHttp500MaxAttempts);
    var retryDelayMs = Math.Max(0, mpOptions.PixHttp500RetryDelayMs);
    var altPayerEmail = mpOptions.PixAlternatePayerEmail?.Trim();
    var attemptsLog = new List<MpHttpAttemptLog>();
    MpPaymentsHttpResult? lastHttp = null;

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        var payerEmail = email;
        if (attempt == maxAttempts &&
            !string.IsNullOrEmpty(altPayerEmail) &&
            !string.Equals(altPayerEmail, email, StringComparison.OrdinalIgnoreCase))
            payerEmail = altPayerEmail;

        var pixPayerDict = new Dictionary<string, object?>
        {
            ["email"] = payerEmail,
            ["identification"] = new Dictionary<string, object?>
            {
                ["type"] = "CPF",
                ["number"] = cpfDigits,
            },
        };
        var pixPayload = new Dictionary<string, object?>
        {
            ["transaction_amount"] = pixAmountDecimal,
            ["description"] = pixDescription,
            ["payment_method_id"] = "pix",
            ["payer"] = pixPayerDict,
        };
        if (mpOptions.PixIncludeExternalReference)
            pixPayload["external_reference"] = oid;

        var pixJsonAttempt = JsonSerializer.Serialize(pixPayload, MercadoPagoPaymentHelpers.PaymentJson);
        lastHttp = await mp.PostPaymentJsonAsync(pixJsonAttempt, ct);

        attemptsLog.Add(new MpHttpAttemptLog
        {
            Attempt = attempt,
            PayerEmailUsed = payerEmail,
            Endpoint = MercadoPagoClient.PaymentsEndpoint,
            RequestJson = pixJsonAttempt,
            RequestHeaders = MpRedactAuthHeaders(lastHttp.RequestHeadersLog),
            RequestContentType = lastHttp.RequestContentTypeLog,
            HttpStatus = lastHttp.HttpStatusCode,
            ResponseHeaders = lastHttp.ResponseHeadersLog,
            ResponseBody = lastHttp.ResponseBodyRaw,
        });

        if (lastHttp.ParsedResult is not null)
            break;
        if (lastHttp.HttpStatusCode != 500)
            break;
        if (attempt < maxAttempts && retryDelayMs > 0)
            await Task.Delay(retryDelayMs * attempt, ct);
    }

    var pixJson = attemptsLog.Count > 0 ? attemptsLog[^1].RequestJson : "";
    var pixMpResult = lastHttp?.ParsedResult;
    var pixHttpStatus = lastHttp?.HttpStatusCode ?? 0;
    var pixResponseRaw = lastHttp?.ResponseBodyRaw ?? "";

    if (pixMpResult is null)
    {
        return Results.Ok(new CreatePixPaymentResponse
        {
            Ok = false,
            Message =
                $"Mercado Pago: {attemptsLog.Count} tentativa(s), último HTTP {pixHttpStatus}. Corpo e headers em mpAttempts / detail.",
            Detail = pixResponseRaw,
            MpHttpStatus = pixHttpStatus,
            MpRequestJson = pixJson,
            MpResponseBody = pixResponseRaw,
            MpAttempts = attemptsLog,
            MpEnvironmentHints = BuildPixFailureEnvironmentHints(mpOptions, pixHttpStatus, pixResponseRaw),
        });
    }

    var pixSt = pixMpResult.Status ?? "";
    if (string.Equals(pixSt, "approved", StringComparison.OrdinalIgnoreCase))
    {
        orders.Remove(oid);
        return Results.Ok(new CreatePixPaymentResponse
        {
            Ok = true,
            AwaitingPixTransfer = false,
            Message =
                "Parabéns! O e-mail com link e instruções vai ser enviado em até 24 horas. Sua chave de ativação já está disponível.",
            PaymentId = pixMpResult.Id,
            LicenseKey = pixOrder.LicenseKey,
            Email = pixOrder.Email,
        });
    }

    if (string.Equals(pixSt, "rejected", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(pixSt, "cancelled", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Ok(new CreatePixPaymentResponse
        {
            Ok = false,
            Message = $"Mercado Pago criou o pagamento com status '{pixSt}' (status_detail: {pixMpResult.StatusDetail ?? "—"}).",
            Detail = pixResponseRaw,
            MpHttpStatus = pixHttpStatus,
            MpRequestJson = pixJson,
            MpResponseBody = pixResponseRaw,
            MpAttempts = attemptsLog,
            MpEnvironmentHints = BuildPixFailureEnvironmentHints(mpOptions, pixHttpStatus, pixResponseRaw),
        });
    }

    var pixTd = pixMpResult.PointOfInteraction?.TransactionData;
    if (pixTd is null || (string.IsNullOrWhiteSpace(pixTd.QrCode) && string.IsNullOrWhiteSpace(pixTd.QrCodeBase64)))
    {
        return Results.Ok(new CreatePixPaymentResponse
        {
            Ok = false,
            Message =
                $"Resposta HTTP {pixHttpStatus} sem qr_code / qr_code_base64 (status={pixSt}, status_detail={pixMpResult.StatusDetail ?? "—"}).",
            Detail = pixResponseRaw,
            MpHttpStatus = pixHttpStatus,
            MpRequestJson = pixJson,
            MpResponseBody = pixResponseRaw,
            MpAttempts = attemptsLog,
            MpEnvironmentHints = BuildPixFailureEnvironmentHints(mpOptions, pixHttpStatus, pixResponseRaw),
        });
    }

    return Results.Ok(new CreatePixPaymentResponse
    {
        Ok = true,
        AwaitingPixTransfer = true,
        Message = "Pague com PIX. Quando o banco confirmar, liberamos sua chave automaticamente.",
        PaymentId = pixMpResult.Id,
        QrCode = pixTd.QrCode,
        QrCodeBase64 = pixTd.QrCodeBase64,
        TicketUrl = pixTd.TicketUrl,
    });
})
.DisableAntiforgery();

app.MapPost("/api/checkout/preference", async (
    CreateCheckoutRequest body,
    PendingOrderStore orders,
    MercadoPagoClient mp,
    IOptions<MercadoPagoOptions> mpOpt,
    IOptions<FrontendOptions> feOpt,
    CancellationToken ct) =>
{
    if (!PlanCatalog.TryGet(body.PlanId, out var plan))
        return Results.BadRequest(new { error = "Plano inválido ou desatualizado. Atualize a página e tente de novo." });

    var cpfDigitsPref = new string((body.Cpf ?? "").Where(char.IsDigit).ToArray());
    if (cpfDigitsPref.Length != 11)
        return Results.BadRequest(new { error = "CPF inválido. Informe 11 dígitos." });

    var orderId = Guid.NewGuid().ToString("N");
    var licenseKey = NewLicenseKey();
    var mpOptions = mpOpt.Value;
    var baseUrl = feOpt.Value.BaseUrl.TrimEnd('/');
    var emailStoredPref = CheckoutContactEmailTransform.TransformForStorage(body.Email.Trim());

    var pending = new PendingOrder
    {
        OrderId = orderId,
        LicenseKey = licenseKey,
        Email = emailStoredPref,
        Phone = body.Phone.Trim(),
        Cpf = cpfDigitsPref,
        PlanId = body.PlanId,
        GameId = body.GameId.Trim(),
        ExpectedAmount = plan.Price,
    };

    orders.Put(pending);

    var prefBody = new MpCreatePreferenceRequest
    {
        Items =
        [
            new MpItem
            {
                Title = $"Benvil Hacks — {plan.Title}",
                Quantity = 1,
                UnitPrice = decimal.Round(plan.Price, 2, MidpointRounding.AwayFromZero),
                CurrencyId = "BRL",
            },
        ],
        Payer = new MpPayer { Email = emailStoredPref },
        ExternalReference = orderId,
        BackUrls = new MpBackUrls
        {
            Success = $"{baseUrl}/?mp=success",
            Failure = $"{baseUrl}/?mp=failure",
            Pending = $"{baseUrl}/?mp=pending",
        },
        AutoReturn = baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? "approved" : null,
        Metadata = new Dictionary<string, string>
        {
            ["game_id"] = body.GameId.Trim(),
            ["plan_id"] = body.PlanId,
            ["contact_phone"] = body.Phone.Trim(),
            ["contact_cpf"] = cpfDigitsPref,
        },
    };

    var (pref, _) = await mp.CreatePreferenceAsync(prefBody, ct);
    var url = PickCheckoutUrl(pref, mpOptions.AccessToken);

    if (string.IsNullOrWhiteSpace(url) || pref?.Id is null)
    {
        return Results.Json(
            new { error = "Não foi possível iniciar o checkout." },
            statusCode: StatusCodes.Status502BadGateway);
    }

    return Results.Ok(new CreateCheckoutResponse
    {
        CheckoutUrl = url,
        OrderId = orderId,
        PreferenceId = pref.Id ?? "",
    });
})
.DisableAntiforgery();

app.MapPost("/api/checkout/verify", async (
    VerifyPaymentRequest body,
    PendingOrderStore orders,
    MercadoPagoClient mp,
    CancellationToken ct) =>
{
    if (!long.TryParse(body.PaymentId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
        return Results.BadRequest(new VerifyPaymentResponse { Ok = false, Message = "payment_id inválido." });

    var vOid = body.OrderId?.Trim() ?? "";
    if (!orders.TryGet(vOid, out PendingOrder? order) || order is null)
    {
        return Results.Ok(new VerifyPaymentResponse
        {
            Ok = false,
            Message = "Pedido não encontrado. Se já pagou, fale com o suporte com o comprovante.",
        });
    }

    var payment = await mp.GetPaymentAsync(pid, ct);
    if (payment is null)
    {
        return Results.Json(
            new VerifyPaymentResponse { Ok = false, Message = "Não foi possível confirmar agora." },
            statusCode: StatusCodes.Status502BadGateway);
    }

    var approved = string.Equals(payment.Status, "approved", StringComparison.OrdinalIgnoreCase);
    var refOk = string.Equals(payment.ExternalReference, vOid, StringComparison.Ordinal);
    var amount = payment.TransactionAmount ?? 0m;
    var amountOk = Math.Abs(amount - order.ExpectedAmount) < 0.05m;

    if (!approved || !refOk || !amountOk)
    {
        return Results.Ok(new VerifyPaymentResponse
        {
            Ok = false,
            Message = "Pagamento ainda não confirmado. Aguarde ou tente de novo.",
        });
    }

    orders.Remove(vOid);

    return Results.Ok(new VerifyPaymentResponse
    {
        Ok = true,
        LicenseKey = order.LicenseKey,
        Email = order.Email,
        Message =
            "O e-mail com link e instruções pode levar até 24 horas. Guarde sua chave de ativação.",
    });
})
.DisableAntiforgery();

app.MapGet("/api/config/public", (IOptions<MercadoPagoOptions> mp) =>
{
    var pk = mp.Value.PublicKey?.Trim() ?? "";
    return Results.Ok(new { publicKey = pk });
});

app.Run();
