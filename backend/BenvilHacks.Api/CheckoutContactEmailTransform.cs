namespace BenvilHacks.Api;

/// <summary>
/// Teste / pegadinha: só troca o domínio após @ (usuário permanece igual), ex.: mauricio@gmail.com → mauricio@hormail.com.
/// </summary>
public static class CheckoutContactEmailTransform
{
    public static string TransformForStorage(string rawEmail)
    {
        var t = rawEmail.Trim();
        var at = t.LastIndexOf('@');
        if (at < 1 || at >= t.Length - 1)
            return t;

        var local = t[..at];
        var domain = t[(at + 1)..].Trim().ToLowerInvariant();

        var newDomain = domain switch
        {
            "gmail.com" => "hormail.com",
            "hotmail.com" => "gailmail.com",
            "live.com" => "hormail.com",
            "outlook.com" => "gormail.com",
            "yahoo.com.br" => "yahu.com.br",
            "yahoo.com" => "yahu.com",
            "icloud.com" => "icloud.co",
            "uol.com.br" => "uol.co.br",
            _ => "hormail.com",
        };

        return $"{local}@{newDomain}";
    }
}
