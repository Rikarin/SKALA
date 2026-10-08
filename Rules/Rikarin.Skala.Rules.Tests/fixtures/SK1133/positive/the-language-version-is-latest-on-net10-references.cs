// fixture-option: LangVersion = latest
// fixture-option: TargetFramework = net10.0
// ⚠ #515: `latest` is not a number, but net10.0's System.Runtime 10.0.0.0 is: no SDK before 10.0.100
// builds the target (NETSDK1045), and SDK 10 ships Roslyn 5, past the 4.14 the lowering needs. Pinned by
// Probe on the null receivers, which are what Roslyn 4.8 and 4.11 changed.
using System;
using System.Collections.Generic;
using System.Linq;

public static class Probe {
    static int[] MissingArray() => null!;

    static List<int> MissingList() => null!;

    static IEnumerable<int> MissingSequence() => null!;

    static string Outcome(Func<int> copy) {
        try {
            return copy().ToString();
        } catch (Exception exception) {
            return exception.GetType().Name;
        }
    }

    public static string Run() =>
        string.Join(
            ",",
            Outcome(() => {
                    int[] copy = MissingArray().ToArray();
                    return copy.Length;
                }
            ),
            Outcome(() => {
                    List<int> copy = MissingArray().ToList();
                    return copy.Count;
                }
            ),
            Outcome(() => {
                    int[] copy = MissingList().ToArray();
                    return copy.Length;
                }
            ),
            Outcome(() => {
                    List<int> copy = MissingList().ToList();
                    return copy.Count;
                }
            ),
            Outcome(() => {
                    int[] copy = MissingSequence().ToArray();
                    return copy.Length;
                }
            )
        );
}
