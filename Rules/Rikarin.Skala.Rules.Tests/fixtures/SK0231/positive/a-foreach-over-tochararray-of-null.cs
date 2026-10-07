// `ToCharArray()` on a null string throws NullReferenceException, and so does the loop over the
// string itself — it reads `Length` before the first element — so the copy goes with no null proof.
public static class Counter {
    public static int Digits(string line) {
        var total = 0;
        foreach (var c in line.ToCharArray()) {
            if (char.IsDigit(c)) {
                total++;
            }
        }

        return total;
    }
}

public static class Probe {
    public static int Run() => Counter.Digits("a1b2") + Counter.Digits(null!);
}
