class C {
    int retries;

    public int Retries {
        get => retries;
        set {
            System.Action apply = () => retries = value;
            apply();
        }
    }
}
