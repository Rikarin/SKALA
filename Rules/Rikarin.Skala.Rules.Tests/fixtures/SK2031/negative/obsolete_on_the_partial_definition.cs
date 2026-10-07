// ⚠ #397: a partial property's attributes may sit on either half and apply to the one member, and
// `[Obsolete]` is conventionally written on the definition — the half this rule, reading the setter's
// body, is never in. The setter that ignores `value` is announced exactly as `obsolete.cs`'s is.
partial class C {
    int legacy;

    [System.Obsolete("Superseded by Retries.")]
    public partial int Attempts { get; set; }

    public partial int Attempts {
        get => legacy;
        set { legacy = 0; }
    }
}
