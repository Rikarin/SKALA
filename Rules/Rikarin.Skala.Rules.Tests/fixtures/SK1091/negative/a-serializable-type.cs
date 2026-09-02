[System.Serializable]
public sealed class Persisted {
    private int Total { get; set; }

    public int Value() {
        Total = 1;
        return Total;
    }
}
