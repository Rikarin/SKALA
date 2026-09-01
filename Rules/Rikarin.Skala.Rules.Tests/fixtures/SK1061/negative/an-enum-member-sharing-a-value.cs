// ⚠ `Status.Finished.ToString()` returns "Done": `Enum.ToString` answers with the first member
// declared with the value, not the one that was written.
public enum Status {
    Done = 1,
    Finished = 1
}

public sealed class Report {
    public string Label() => Status.Finished.ToString();
}
