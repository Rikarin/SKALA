// A loop variable written as the enum, or as `object`, reads the same element over `Array` and over
// `T[]`; the length and `Cast<T>()` bind the same members on both. Pinned by Probe (#425).
using System;
using System.Linq;

public enum Color {
    Red,
    Green
}

public static class Probe {
    public static string Run() {
        var text = "";
        foreach (Color value in Enum.GetValues(typeof(Color))) {
            text += value + ";";
        }

        foreach (object value in Enum.GetValues(typeof(Color))) {
            text += value.GetType().Name + ";";
        }

        return text + Enum.GetValues(typeof(Color)).Length + Enum.GetValues(typeof(Color)).Cast<Color>().Last();
    }
}
