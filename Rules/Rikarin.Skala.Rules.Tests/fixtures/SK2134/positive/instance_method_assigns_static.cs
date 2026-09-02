// Not a constructor, and the same defect: whichever instance was configured last decides the
// timeout for all of them.
sealed class Client {
    static int timeout = 30;

    public void Configure(int seconds) {
        timeout = seconds;
    }

    public int Timeout => timeout;
}
