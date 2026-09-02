// Instance code writing instance state is the ordinary case, and the one the rule must never touch.
sealed class Client {
    int timeout = 30;

    public void Configure(int seconds) {
        timeout = seconds;
    }

    public int Timeout => timeout;
}
