class C {
    string M(int n) {
        if (n < 10) {
            return "small";
        } else if (n < 100) {
            return "medium";
        } else if (n < 1000) {
            return "large";
        }

        return "huge";
    }
}
