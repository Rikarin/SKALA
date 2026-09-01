// ⚠ SK2012 already reports the automatic-property case, with a proof about the accessor bodies
// that this rule does not have. Reporting both would be one defect counted twice.
class C {
    int Value { get; set; }

    bool M() => Value == Value;
}
