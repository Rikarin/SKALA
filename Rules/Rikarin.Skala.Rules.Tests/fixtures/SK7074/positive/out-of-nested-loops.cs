public sealed class Work {
    public bool Run(int[][] rows) {
        foreach (var row in rows) {
            foreach (var cell in row) {
                if (cell < 0) {
                    goto broken;
                }
            }
        }

        return true;

    broken:
        return false;
    }
}
