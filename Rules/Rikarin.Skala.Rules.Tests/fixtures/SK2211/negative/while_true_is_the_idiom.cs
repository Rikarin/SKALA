// ⚠ An infinite loop is usually the point. `while (true)` is the event loop, the reactor and the
// retry pump, and a rule that reported it would be reporting the shape every server is built out of.
// The finding is a condition that *could* have changed and does not, so a constant condition is
// never one.
class C {
    void Pump() {
        while (true) {
            System.Console.WriteLine("tick");
        }
    }

    void Forever() {
        for (;;) {
            System.Console.WriteLine("tick");
        }
    }
}
