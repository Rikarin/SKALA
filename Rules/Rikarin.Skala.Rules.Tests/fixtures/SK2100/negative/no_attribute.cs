using System.Text;

// A static field with an initializer and no attribute is the ordinary case, not a finding.
static class Shared {
    static StringBuilder buffer = new StringBuilder();

    public static StringBuilder Buffer => buffer;
}
