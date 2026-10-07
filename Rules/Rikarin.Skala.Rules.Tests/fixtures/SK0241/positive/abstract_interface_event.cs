using System;

// An ordinary interface event is abstract whether or not the keyword is written.
interface INotifier {
    abstract event Action Changed;
}
