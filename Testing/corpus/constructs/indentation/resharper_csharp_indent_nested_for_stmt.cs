class C {
    void M() {
        for (var i = 0; i < 4; i++)
        for (var j = 0; j < 4; j++) {
            M();
        }
    }
}
