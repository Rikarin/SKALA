class BinaryPatterns {
    bool M(object o) => o is int
        or string
        or bool;

    bool N(object o) => o is int or
        string or
        bool;
}
