public sealed class Annotated {
    int count;

    // The fix deletes everything between the target and the operator, so a comment there is text
    // it would silently take with it.
    public void Advance() {
        count = count /* one per call */ + 1;
    }

    public int Value => count;
}
