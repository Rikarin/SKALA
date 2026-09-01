using System;
using System.Text;

// ⚠ The initializer runs once, in the static constructor, on whichever thread touches the type
// first. That thread sees the builder; every other thread sees null.
static class Scratch {
    [ThreadStatic] static StringBuilder buffer = new StringBuilder();

    public static StringBuilder Buffer => buffer;
}
