public sealed record LogRecord(string Message);

public sealed record DnsRecord(string Host, string Address);

public sealed class AuditRecord {
    public string Actor { get; init; } = string.Empty;
}

public readonly record struct ActivationRecord(int Depth);
