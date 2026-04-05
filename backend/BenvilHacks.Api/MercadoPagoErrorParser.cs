using System.Text.Json;
using System.Text.Json.Serialization;

namespace BenvilHacks.Api;

internal static class MercadoPagoPaymentHelpers
{
    public static readonly JsonSerializerOptions PaymentJson = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string SanitizeDescription(string text) =>
        text.Replace("\u2014", "-", StringComparison.Ordinal).Replace("\u2013", "-", StringComparison.Ordinal);

    /// <summary>Limita tamanho da descrição (API de pagamentos do MP rejeita textos longos).</summary>
    public static string TruncatePaymentDescription(string text, int maxLen = 200)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLen)
            return text;
        return text[..maxLen].TrimEnd();
    }
}
