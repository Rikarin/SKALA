class Options {
    public bool verbose;
}

class C {
    bool M(Options options) => options.verbose || options.verbose;
}
