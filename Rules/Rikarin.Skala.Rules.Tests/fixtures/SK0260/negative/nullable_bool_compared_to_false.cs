class C {
    public static int Run(bool? maybe) {
        if (maybe != false) {
            return 1;
        }

        return 0;
    }
}
