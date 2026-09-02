public sealed class Session {
    readonly int retries = 5;

    public Session(int given) {
        retries = given;
    }

    public int Retries => retries;
}
