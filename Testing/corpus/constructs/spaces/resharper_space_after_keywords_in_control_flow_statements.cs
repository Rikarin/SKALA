class C {
    void M(bool b) {
        if (b) {
            M(b);
        }

        while (b) {
            M(b);
        }

        switch (b) {
            case true:
                break;
        }
    }
}
