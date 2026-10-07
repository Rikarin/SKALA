// A tuple element is written however deep it sits, and through a parenthesis.
namespace Fixtures {
    sealed class Cursor(int line, int column) {
        public void Reset() {
            ((line), (column, var unused)) = (0, (0, 0));
        }

        public string Where => line + ":" + column;
    }
}
