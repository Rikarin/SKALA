class C {
    public static int Run(bool ready) {
        if (ready /* the explicit form is deliberate */ == true) {
            return 1;
        }

        return 0;
    }
}
