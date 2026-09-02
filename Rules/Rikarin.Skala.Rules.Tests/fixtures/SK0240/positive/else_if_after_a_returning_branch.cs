class C {
    public static int Pick(int value) {
        if (value < 0) {
            return -1;
        } else if (value > 0) {
            return 1;
        }

        return 0;
    }
}
