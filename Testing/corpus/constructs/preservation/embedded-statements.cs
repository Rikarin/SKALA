class EmbeddedStatements {
    void M(bool flag) {
        if (flag)
            First();

        if (flag) Second();

        while (flag)
            Third();

        foreach (var item in items)
            Use(item);
    }
}
