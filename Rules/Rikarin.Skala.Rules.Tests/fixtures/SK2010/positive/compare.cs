class C {
    bool M(string left, string right) => string.Compare(left, right) == 0;
    int N(string left, string right) => string.Compare(left, right, true);
}
