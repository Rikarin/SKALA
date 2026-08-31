// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-31
using System;

// EventDeclaration, AddAccessorDeclaration, RemoveAccessorDeclaration and UnknownAccessorDeclaration
// occurred nowhere in the corpus. An accessor-holder is a braced block with its own arrangement keys,
// so the shapes below are the ones those keys disagree about: block-bodied, expression-bodied, and
// one long enough that the accessor's own body has to wrap.
class EventAccessors {
    readonly object gate = new object();
    EventHandler? changed;

    public event EventHandler Changed {
        add {
            lock (gate) {
                changed += value;
            }
        }
        remove {
            lock (gate) {
                changed -= value;
            }
        }
    }

    public event EventHandler<string> Expressed {
        add => changed += (sender, arguments) => value(sender, arguments.ToString() ?? string.Empty);
        remove => changed -= (sender, arguments) => value(sender, arguments.ToString() ?? string.Empty);
    }

    public event Action<string, int, bool, long, double> Overflowing {
        add =>
            throw new NotSupportedException(
                "this event is declared for the accessor shape alone and cannot be subscribed to"
            );
        remove { }
    }

    // The interface-implementing form, where the accessor holder carries an explicit specifier.
    event EventHandler IHaveAnEvent.Explicit {
        add { changed += value; }
        remove { changed -= value; }
    }

    public event EventHandler? Field;

    public event EventHandler? FirstOfTwo, SecondOfTwo;
}

interface IHaveAnEvent {
    event EventHandler Explicit;
}
