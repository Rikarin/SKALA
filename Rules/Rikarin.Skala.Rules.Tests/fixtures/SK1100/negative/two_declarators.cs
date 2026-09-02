public sealed class Paired {
    // Joining one declarator and leaving the other where it stands is a different edit, so a
    // declaration holding two of them is declined outright.
    public static int Second() {
        int first = 1, second = 2;
        return second;
    }
}
