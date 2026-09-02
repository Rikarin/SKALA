class C {
    public static int Run(int left, int right, bool other) {
        if (other && !(left == right)) {
            return 1;
        }

        return 0;
    }
}
