using System;

// The shape the rule exists for: a decision that depends on the time, with no way for a caller or a
// test to say what the time is.
public sealed class TokenPolicy {
    public bool IsExpired(DateTime expiresAt) => expiresAt < DateTime.UtcNow;
}
