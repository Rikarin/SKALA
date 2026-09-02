// ⚠ A `ref` local's value can change through the alias without any write appearing in the body,
// so both spellings are declined.
class C {
    int budget;

    void RefLocal() {
        ref var remaining = ref this.budget;
        while (remaining > 0) {
            Spend();
        }
    }

    void RefParameter(ref int remaining) {
        while (remaining > 0) {
            Spend();
        }
    }

    void Spend() => this.budget--;
}
