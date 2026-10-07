// A list, a string and an array each have one size, so the subtraction and `^` read the same element —
// and a negative named offset throws the same exception both ways, because the compiler lowers `a[^n]`
// on these types to the subtraction itself. Pinned by Probe (#425).
using System.Collections.Generic;

public static class Probe {
    public static string Run() {
        var list = new List<int> { 1, 2, 3 };
        var text = "abc";
        int[] array = { 4, 5, 6 };
        var n = 2;
        var result = list[list.Count - 1] + "/" + text[text.Length - n] + "/" + array[array.Length - n];
        var minus = -1;
        try {
            result += array[array.Length - minus];
        } catch (System.Exception thrown) {
            result += "/" + thrown.GetType().Name;
        }

        return result;
    }
}
