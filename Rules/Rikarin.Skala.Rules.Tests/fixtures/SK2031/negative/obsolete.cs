class C {
    int legacy;

    [System.Obsolete("Superseded by Retries.")]
    public int Attempts {
        get => legacy;
        set { legacy = 0; }
    }
}
