public sealed class Gate {
    public bool Open(int value) {
        if (value is not not > 0) {
            return true;
        }

        return false;
    }
}
