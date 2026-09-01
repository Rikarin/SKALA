// ⚠ `string == string` looks like a user-defined operator and is not one: Roslyn models it as a
// built-in equality whose `OperatorMethod` is null, so it reaches the rule and is reported. It is
// always true, so it is a true positive — but the first draft's prose claimed the opposite.
class C {
    bool M(string a) => a == a;
}
