using System;

// The repaired shape, and the one the rule's message asks for.
public sealed class TokenPolicy {
    readonly TimeProvider time;

    public TokenPolicy(TimeProvider time) => this.time = time;

    public bool IsExpired(DateTimeOffset expiresAt) => expiresAt < time.GetUtcNow();
}
