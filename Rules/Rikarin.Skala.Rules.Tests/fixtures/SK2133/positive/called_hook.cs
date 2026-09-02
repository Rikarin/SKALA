// Nothing implements `OnCreated`, so the call below is deleted along with the declaration. The line
// reads as a hook and runs nothing.
partial class Importer {
    partial void OnCreated();

    public void Create() {
        OnCreated();
    }
}
