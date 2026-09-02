// A property is a method call and a method call is anything at all. `queue.Count` changes because
// something dequeues, and `reader.Read()` advances the reader as a side effect of being asked.
using System.Collections.Generic;
using System.IO;

class C {
    void Drain(Queue<int> queue) {
        while (queue.Count > 0) {
            queue.Dequeue();
        }
    }

    void Consume(TextReader reader) {
        while (reader.Peek() >= 0) {
            reader.Read();
        }
    }
}
