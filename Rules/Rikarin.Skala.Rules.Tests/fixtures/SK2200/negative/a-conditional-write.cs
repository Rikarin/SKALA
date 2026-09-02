// The assignment is not unconditional, so on the other path the declared value is the one used.
public sealed class Retry {
    int attempts = 3;

    public Retry(int given) {
        if (given > 0) {
            attempts = given;
        }
    }

    public int Attempts => attempts;
}
