using System;

// A value arriving from a parameter, a field or a property has a `Kind` nothing here can prove, and an
// unproved `Kind` is silence. This is the rule's largest stated gap and the price of never guessing.
public sealed class Schedule {
    DateTime stored;

    public DateTime FromParameter(DateTime value) => value.ToUniversalTime();

    public DateTime FromField() => stored.ToUniversalTime();

    public DateTime FromProperty => Stored.ToLocalTime();

    DateTime Stored => stored;
}
