public static class Walks {
    public static int Count(char[] chars) {
        var total = 0;
        foreach (var c in chars) {
            if (c != ' ') {
                total++;
            }
        }

        return total;
    }
}
