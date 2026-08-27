class EmbeddedBlocks {
    void M(bool flag) {
        if (flag) {
            First();
        }

        if (flag) {
            Second();
        }

        while (flag) {
            Third();
        }
    }
}
