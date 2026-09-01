public static class Counter {
    // ToCharArray copies the whole string so that foreach can walk what string already indexes.
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
