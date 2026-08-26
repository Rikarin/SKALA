class C {
    int A { get; set; }

    C M() => new() {
        A = 1,

        A = 2
    };
}
