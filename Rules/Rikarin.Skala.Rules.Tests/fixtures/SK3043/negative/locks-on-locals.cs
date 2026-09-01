public sealed class Transfer {
    int moved;

    public void Move(object first, object second) {
        // Parameters say nothing about which object they currently point at, so no order can be
        // read off them. A caller that always passes them in a canonical order is correct, and the
        // rule has no way to tell that apart from a caller that does not.
        lock (first) {
            lock (second) {
                moved++;
            }
        }
    }

    public void Undo(object first, object second) {
        lock (second) {
            lock (first) {
                moved--;
            }
        }
    }
}
