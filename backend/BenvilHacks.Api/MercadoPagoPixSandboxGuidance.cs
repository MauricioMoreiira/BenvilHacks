namespace BenvilHacks.Api;

/// <summary>Modo de credenciais (PIX/cartão). O POST /v1/payments não muda; só orienta qual par usar no painel.</summary>
internal static class MercadoPagoPixSandboxGuidance
{
    public const string ModeTestKeysRealAccount = "test_keys_real_account";
    public const string ModeProductionKeysTestSeller = "production_keys_test_seller";
    public const string ModeProductionRealAccount = "production_real_account";

    public static string NormalizeMode(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? ModeProductionRealAccount : raw.Trim();

    public static bool IsProductionKeysTestSellerMode(string? mode) =>
        string.Equals(NormalizeMode(mode), ModeProductionKeysTestSeller, StringComparison.OrdinalIgnoreCase);

    public static bool IsProductionRealAccountMode(string? mode) =>
        string.Equals(NormalizeMode(mode), ModeProductionRealAccount, StringComparison.OrdinalIgnoreCase);

    public static bool TokenMatchesConfiguredMode(string accessToken, string? mode)
    {
        var t = accessToken.Trim();
        if (t.Length == 0)
            return false;
        var test = t.StartsWith("TEST-", StringComparison.OrdinalIgnoreCase);
        if (IsProductionRealAccountMode(mode) || IsProductionKeysTestSellerMode(mode))
            return !test;
        return test;
    }

    /// <summary>Uma linha para o front (checkout) — sem URLs longas.</summary>
    public static IReadOnlyList<string> HintsForCurrentConfig(string? mode, string accessToken)
    {
        var m = NormalizeMode(mode);
        if (IsProductionRealAccountMode(m))
            return ["Produção: use Public Key e Access Token de credenciais de produção no painel Mercado Pago."];
        if (IsProductionKeysTestSellerMode(m))
            return ["Vendedor de teste: use APP_USR/PK de produção da conta em Contas de teste."];
        return ["Teste: use credenciais TEST- da aplicação (Credenciais de teste no painel)."];
    }

    public static IReadOnlyList<string> ExtraHintsOnPixHttpFailure(string? mode, string accessToken, int httpStatus)
    {
        var list = new List<string>();
        var tok = accessToken.Trim();
        var testTok = tok.StartsWith("TEST-", StringComparison.OrdinalIgnoreCase);

        if (httpStatus == 500 && testTok && !IsProductionKeysTestSellerMode(mode))
        {
            list.Add(
                "HTTP 500 no PIX com TEST-: tente PixSandboxMode production_keys_test_seller e APP_USR do vendedor de teste.");
        }

        if (httpStatus == 500 && IsProductionKeysTestSellerMode(mode) && !testTok)
            list.Add("HTTP 500: confira PIX na conta MP e status em status.mercadopago.com");

        return list;
    }
}
