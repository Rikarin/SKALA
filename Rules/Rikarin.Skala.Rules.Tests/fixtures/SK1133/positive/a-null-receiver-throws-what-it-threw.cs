// fixture-option: LangVersion = 14
// ⚠ #512: the spread lowers to the call it replaces, so a null receiver throws the same exception —
// ArgumentNullException from Enumerable, NullReferenceException from List<T>.ToArray. Roslyn 4.8 threw
// NullReferenceException for all of them and Roslyn 4.11 returned an empty array for a null array,
// which is why the floor is C# 14. Pinned by Probe.
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
