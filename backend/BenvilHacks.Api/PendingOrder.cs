namespace BenvilHacks.Api;

public sealed class PendingOrder
{
    public required string OrderId { get; init; }
    public required string LicenseKey { get; init; }
    public required string Email { get; init; }
    public required string Phone { get; init; }
    /// <summary>CPF somente dígitos (11).</summary>
    public required string Cpf { get; init; }
    public required string PlanId { get; init; }
    public required string GameId { get; init; }
    public required decimal ExpectedAmount { get; init; }
}
