// An ordinary private method with a body is not partial and is not erased.
partial class Importer {
    void OnCreated() {
    }

    public void Create() {
        OnCreated();
    }
}
