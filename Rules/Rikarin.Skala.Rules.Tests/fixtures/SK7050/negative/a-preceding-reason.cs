// The fixture deliberately exercises an unused local.
#pragma warning disable CS0168
public sealed class Work {
    public void Run() {
        int unused;
    }
}
#pragma warning restore CS0168
