class InvocationParens {
    void M() {
        OneArgumentBrokenAtTheParen(
            first);
        TwoArgumentsBrokenBetweenThem(first,
            second);
        TwoArgumentsBrokenAtTheParenAndBetweenThem(
            first,
            second);
        NothingBroken(first, second);
        new Constructed(
            first,
            second);
    }
}
