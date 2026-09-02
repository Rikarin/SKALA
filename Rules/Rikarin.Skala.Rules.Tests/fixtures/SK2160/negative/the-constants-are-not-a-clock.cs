using System;

// `MinValue` and `MaxValue` are constants, not clock reads. Reporting them would make every sentinel
// comparison a finding.
public sealed class Range {
    public DateTime Floor { get; } = DateTime.MinValue;

    public DateTime Ceiling { get; } = DateTime.MaxValue;

    public bool IsUnset(DateTime value) => value == default;
}
