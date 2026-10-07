using System;

// ⚠ Both shapes are on one `try`, and they are answered by one finding carrying one edit. Reported
// separately their two deletions compose into `try { … }`, which is CS1524; reported one per pass,
// the fix's own output still carries the other finding. The clause that would survive is nothing, so
// the edit is the unwrap.
class C {
    static readonly int[] Values = new int[4];
    static int _value;

    public static void Read(int index) {
        try {
            _value = Values[index];
        } catch (IndexOutOfRangeException) {
            throw;
        } finally {
        }
    }

    public static int Value => _value;
}
