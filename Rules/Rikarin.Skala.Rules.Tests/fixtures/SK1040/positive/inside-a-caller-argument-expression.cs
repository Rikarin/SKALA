// #422: the argument's source text is the exception's parameter name, so `int?` there would change
// what Probe.Run() returns. The finding stands; its fix is safe on the field and not in the argument.
using System;

public static class Probe {
    static readonly Nullable<int> Missing = null;

    public static string Run() {
        try {
            ArgumentNullException.ThrowIfNull(default(Nullable<int>) ?? Missing);
            return "no throw";
        } catch (ArgumentNullException e) {
            return e.ParamName ?? "";
        }
    }
}
