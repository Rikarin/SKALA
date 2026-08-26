class C {
    void M(int a) {
        switch (a) {
            case 1: {
                M(a);
                break;
            }
            case 2:
                break;
        }
    }
}
