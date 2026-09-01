// The inner `!(…)` sits under another `!`, where a bare `value is not string` would parse as
// `(!value) is not string`. The rule declines the position rather than inventing parentheses.
public sealed class Inspector {
    public bool IsText(object value) => !!(value is string);
}
