// The spellings that work. `^1` is the last element, which is what `^0` was reaching for.
using System.Collections.Generic;

class C {
    int Last(int[] values) => values[^1];

    char LastChar(string text) => text[^1];

    int First(List<int> values) => values[0];

    int[] Slice(int[] values) => values[1..3];
}
