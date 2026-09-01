class C {
    public int ReadOnlyView {
        get => 0;
        set => throw new System.NotSupportedException();
    }

    public int AlsoReadOnly {
        get => 0;
        set { throw new System.NotSupportedException(); }
    }
}
