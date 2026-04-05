using System.Text.Json;

namespace BenvilHacks.Api;

/// <summary>
/// Pedidos pendentes em disco — sobrevive a reinício da API e a <c>dotnet watch</c>.
/// (IMemoryCache perdia tudo a cada reload e gerava "Pedido expirado" indevidamente.)
/// </summary>
public sealed class PendingOrderStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private static readonly TimeSpan Ttl = TimeSpan.FromHours(4);

    private readonly string _dir;
    private readonly ILogger<PendingOrderStore> _log;
    private readonly object _sync = new();

    public PendingOrderStore(IWebHostEnvironment env, ILogger<PendingOrderStore> log)
    {
        _dir = Path.Combine(env.ContentRootPath, "Data", "pending-orders");
        _log = log;
    }

    public void EnsureDirectory()
    {
        lock (_sync)
        {
            Directory.CreateDirectory(_dir);
        }
    }

    public void Put(PendingOrder order)
    {
        EnsureDirectory();
        var id = SanitizeOrderId(order.OrderId);
        if (id is null)
        {
            _log.LogWarning("OrderId inválido ao gravar pedido.");
            return;
        }

        var record = new PendingOrderRecord
        {
            OrderId = order.OrderId,
            LicenseKey = order.LicenseKey,
            Email = order.Email,
            Phone = order.Phone,
            Cpf = order.Cpf,
            PlanId = order.PlanId,
            GameId = order.GameId,
            ExpectedAmount = order.ExpectedAmount,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        var path = Path.Combine(_dir, $"{id}.json");
        var json = JsonSerializer.Serialize(record, JsonOpts);
        lock (_sync)
        {
            File.WriteAllText(path, json);
        }
    }

    public bool TryGet(string orderId, out PendingOrder? order)
    {
        order = null;
        var id = SanitizeOrderId(orderId);
        if (id is null)
        {
            _log.LogInformation(
                "TryGet: OrderId inválido na sanitização (vazio/formato) lenEntrada={Len}",
                orderId?.Length ?? 0);
            return false;
        }

        var path = Path.Combine(_dir, $"{id}.json");
        lock (_sync)
        {
            if (!File.Exists(path))
                return false;

            try
            {
                var json = File.ReadAllText(path);
                var rec = JsonSerializer.Deserialize<PendingOrderRecord>(json, JsonOpts);
                if (rec is null)
                    return false;

                if (DateTimeOffset.UtcNow - rec.CreatedAtUtc > Ttl)
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch (Exception ex)
                    {
                        _log.LogDebug(ex, "Falha ao remover pedido expirado.");
                    }

                    return false;
                }

                order = rec.ToPendingOrder();
                return true;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Falha ao ler pedido {OrderId}", id);
                return false;
            }
        }
    }

    public void Remove(string orderId)
    {
        var id = SanitizeOrderId(orderId);
        if (id is null)
            return;

        var path = Path.Combine(_dir, $"{id}.json");
        lock (_sync)
        {
            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception ex)
                {
                    _log.LogDebug(ex, "Falha ao remover arquivo do pedido.");
                }
            }
        }
    }

    private static string? SanitizeOrderId(string? orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            return null;
        var t = orderId.Trim();
        if (t.Length is < 8 or > 64)
            return null;
        if (t.Contains("..", StringComparison.Ordinal) ||
            t.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return null;
        // Pedidos são Guid format N (32 hex) — só permite hex
        foreach (var c in t)
        {
            if (IsHex(c))
                continue;
            return null;
        }

        return t.ToLowerInvariant();
    }

    private static bool IsHex(char c) =>
        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    private sealed class PendingOrderRecord
    {
        public string OrderId { get; set; } = "";
        public string LicenseKey { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Cpf { get; set; } = "";
        public string? Discord { get; set; }
        public string PlanId { get; set; } = "";
        public string GameId { get; set; } = "";
        public decimal ExpectedAmount { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }

        public PendingOrder ToPendingOrder()
        {
            var cpfDigits = new string(Cpf.Where(char.IsDigit).ToArray());
            return new PendingOrder
            {
                OrderId = OrderId,
                LicenseKey = LicenseKey,
                Email = Email,
                Phone = Phone,
                Cpf = cpfDigits,
                PlanId = PlanId,
                GameId = GameId,
                ExpectedAmount = ExpectedAmount,
            };
        }
    }
}
