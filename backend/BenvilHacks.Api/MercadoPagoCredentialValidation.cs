using System.Text.RegularExpressions;

namespace BenvilHacks.Api;

internal static class MercadoPagoCredentialValidation
{
    /// <summary>Public Key de produção costuma ser APP_USR- + UUID. Isso NÃO funciona como Bearer no POST /v1/payments.</summary>
    private static readonly Regex AppUsrPublicKeyShape = new(
        @"^APP_USR-[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool LooksLikeAppUsrPublicKey(string? value) =>
        AppUsrPublicKeyShape.IsMatch((value ?? "").Trim());

    /// <summary>Access Token APP_USR típico: vários segmentos, bem mais longo que a Public Key.</summary>
    public static bool LooksLikeAppUsrAccessToken(string? value)
    {
        var v = (value ?? "").Trim();
        if (!v.StartsWith("APP_USR-", StringComparison.OrdinalIgnoreCase))
            return false;
        if (LooksLikeAppUsrPublicKey(v))
            return false;
        return v.Length >= 48;
    }

    public static bool AccessTokenIsTest(string? token) =>
        (token ?? "").Trim().StartsWith("TEST-", StringComparison.OrdinalIgnoreCase);

    public static bool PublicKeyIsTest(string? publicKey) =>
        (publicKey ?? "").Trim().StartsWith("TEST-", StringComparison.OrdinalIgnoreCase);

    public static string AccessTokenKind(string? token)
    {
        var t = (token ?? "").Trim();
        if (t.Length == 0)
            return "missing";
        if (t.StartsWith("TEST-", StringComparison.OrdinalIgnoreCase))
            return "test";
        if (t.StartsWith("APP_USR-", StringComparison.OrdinalIgnoreCase))
            return "production_app_usr";
        return "unknown";
    }

    public static bool MustRejectTestCredentials(MercadoPagoOptions o) =>
        o.RejectTestMercadoPagoCredentials ||
        MercadoPagoPixSandboxGuidance.IsProductionKeysTestSellerMode(o.PixSandboxMode) ||
        MercadoPagoPixSandboxGuidance.IsProductionRealAccountMode(o.PixSandboxMode);

    public static IEnumerable<string> SplitSemicolonOrCommaList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            yield break;
        foreach (var part in raw.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var p = part.Trim();
            if (p.Length > 0)
                yield return p;
        }
    }

    /// <summary>True se o e-mail do payer viola blocklist ou sufixo obrigatório (conta compradora de teste).</summary>
    public static bool PayerEmailViolatesRules(string payerEmail, MercadoPagoOptions o, out string errorMessage)
    {
        errorMessage = "";
        var e = payerEmail.Trim();

        foreach (var blocked in SplitSemicolonOrCommaList(o.PixPayerEmailBlocklist))
        {
            if (string.Equals(e, blocked, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage =
                    "Este e-mail não pode ser usado como pagador no PIX (conta real do vendedor). Use o e-mail da conta compradora de teste do Mercado Pago.";
                return true;
            }
        }

        var suffix = o.PixPayerEmailMustEndWith?.Trim();
        if (!string.IsNullOrEmpty(suffix) && !e.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            errorMessage =
                $"No PIX, use o e-mail da conta compradora de teste (deve terminar com \"{suffix}\").";
            return true;
        }

        return false;
    }
}
