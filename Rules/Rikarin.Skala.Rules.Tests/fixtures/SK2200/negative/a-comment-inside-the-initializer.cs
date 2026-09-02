public sealed class Timeout {
    readonly int seconds = /* the documented default */ 30;

    public Timeout(int given) {
        seconds = given;
    }

    public int Seconds => seconds;
}
