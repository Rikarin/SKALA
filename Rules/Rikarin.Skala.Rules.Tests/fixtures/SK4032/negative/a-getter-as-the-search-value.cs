// ⚠ #423: `IndexOf(Needle, Start, …)` evaluates the search value before the offset, and the original
// evaluated it after. Measured for #412's audit with two logging getters: "Start, Needle" before the
// fix, "Needle, Start" after.
public static class Probe {
    static string log = "";

    static int Start {
        get {
            log += "S";
            return 1;
        }
    }

    static string Needle {
        get {
            log += "N";
            return "c";
        }
    }

    public static string Run() {
        var found = "abcabc".Substring(Start).IndexOf(Needle, System.StringComparison.Ordinal) >= 0;
        return log + found;
    }
}
