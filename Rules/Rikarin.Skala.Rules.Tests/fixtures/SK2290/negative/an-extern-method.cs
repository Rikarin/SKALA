using System;
using System.Runtime.InteropServices;

// An `extern` declaration has no body, so there is no dead computation to point at and the parameter
// list is the platform's rather than the author's. Reporting it would ask for an edit to a signature
// the runtime marshals.
class Native {
    [DllImport("kernel32")]
    private static extern bool QueryPerformanceCounter(out long value);

    public void Probe() {
        if (QueryPerformanceCounter(out _)) {
            Console.WriteLine("counted");
        }
    }
}
