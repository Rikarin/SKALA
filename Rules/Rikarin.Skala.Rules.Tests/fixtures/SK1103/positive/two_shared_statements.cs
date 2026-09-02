public sealed class Sending {
    static void Delay() { }

    static void Prepare() { }

    static void Send() { }

    static void Flush() { }

    public static void Dispatch(bool retry) {
        if (retry) {
            Delay();
            Send();
            Flush();
        } else {
            Prepare();
            Send();
            Flush();
        }
    }
}
