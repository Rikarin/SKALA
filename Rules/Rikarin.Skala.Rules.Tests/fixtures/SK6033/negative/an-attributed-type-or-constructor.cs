using System;

namespace Contoso.Design;

[Serializable]
public sealed class Payload {
    private Payload() { }

    public string Body { get; init; } = string.Empty;
}

public sealed class Envelope {
    [Obsolete("Deserialization only.")]
    private Envelope() { }

    public string Body { get; init; } = string.Empty;
}
