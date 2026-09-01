// ⚠ A `restore` naming other ids does not close this `disable`. Ending the region at the wrong
// directive would report a bracket that is holding real code.
public sealed class Work {
#pragma warning disable CS0168 // The fixture deliberately exercises an unused local.
#pragma warning restore CS0219
    public void Run() {
        int unused;
    }
#pragma warning restore CS0168
}
