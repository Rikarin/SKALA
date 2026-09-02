// A nullable reference type satisfies CS8618 by being nullable, so the compiler is silent and the
// property is still permanently null.
sealed class Request {
    public string? Trace { get; }

    public bool Traced => Trace is not null;
}
