namespace Fixtures {
    sealed class Window(int width) {
        public int Width {
            get => width;
            set => width = value;
        }
    }
}
