// A `ref` return hands out storage the caller may write through, so it is invariant and not
// covariant. This is the one shape here that reads as an ordinary return type and is not.
public interface ISlot<T> {
    ref T Slot();
}
