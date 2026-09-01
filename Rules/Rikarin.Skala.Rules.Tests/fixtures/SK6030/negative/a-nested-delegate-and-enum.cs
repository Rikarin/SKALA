namespace Contoso.Diagnostics {
    public static class Probe {
        public delegate void Sampled(int value);

        public enum State {
            Idle = 0,
            Running = 1
        }
    }
}
