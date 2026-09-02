// Reading one side as "the redundant one" would leave the other literal standing on its own.
class C {
    public static bool Run() => true == false;
}
