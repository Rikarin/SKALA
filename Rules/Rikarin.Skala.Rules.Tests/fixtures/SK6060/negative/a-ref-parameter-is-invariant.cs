public interface IFiller<T> {
    void Fill(ref T value);

    void Take(out T value);
}
