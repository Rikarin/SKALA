// A property getter is a method, so two evaluations are two calls and this rule proves nothing
// about them. It is also the one comparison shape the compiler leaves silent, and SK2012 owns it.
class C {
    bool Ready { get; set; }

    int Count { get; set; }

    bool M() => Ready && Ready;

    int N() => Count - Count;
}
