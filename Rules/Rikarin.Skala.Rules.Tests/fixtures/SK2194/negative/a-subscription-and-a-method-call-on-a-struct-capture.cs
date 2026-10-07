// ⚠ Neither is an assignment, and `readonly struct` compiles both: `+=` on an event calls its `add`
// accessor and `Increment` is a method. Whether they mutate the capture is SK4022's question, not
// this rule's (#412). `+=` on an event counted here from #408 until #412.
using System;

namespace Fixtures {
    public struct Signal {
        public int Listeners;

        public event Action Raised {
            add => Listeners++;
            remove => Listeners--;
        }

        public void Increment() => Listeners++;
    }

    struct Relay(Signal signal) {
        public void Listen(Action handler) {
            signal.Raised += handler;
            signal.Increment();
        }

        public int Listeners => signal.Listeners;
    }
}
