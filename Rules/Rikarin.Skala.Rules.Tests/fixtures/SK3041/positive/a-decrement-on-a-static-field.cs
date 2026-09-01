public static class Pool {
    static volatile int available = 8;

    public static void Take() {
        --available;
    }

    public static int Available => available;
}
