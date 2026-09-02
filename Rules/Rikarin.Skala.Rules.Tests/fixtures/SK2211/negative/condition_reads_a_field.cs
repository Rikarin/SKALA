// ⚠ The exclusion that is the whole false-positive story. `stopped` is written by another thread,
// another method, or the object's own constructor, and none of those writes appears in this body.
// Only locals and parameters have a writer set this analysis can enumerate.
class C {
    bool stopped;

    void Poll() {
        while (!this.stopped) {
            System.Threading.Thread.Sleep(1);
        }
    }
}
