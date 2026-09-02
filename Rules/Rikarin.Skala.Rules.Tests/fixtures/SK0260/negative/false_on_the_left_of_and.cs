// `false && ready` never evaluates `ready`; replacing it with `ready` would start doing so.
class C {
    public static bool Run(bool ready) => false && ready;
}
