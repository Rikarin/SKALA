using System;

interface INotifier {
    event Action Changed {
        add { }
        remove { }
    }
}

interface IStrictNotifier : INotifier {
    abstract event Action INotifier.Changed;
}
