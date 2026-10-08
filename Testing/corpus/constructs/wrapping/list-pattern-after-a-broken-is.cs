// #562: a one-line list pattern after an `is` the author broke after keeps the break.
class C562 {
  object A() => xs is
[1, 2];
  object B() => xs is
      [1, 2];
  object C() =>
      xs is
      [1, 2];
  object D() =>
      xs is
          [1, 2];
  object E() =>
xs is
[1, 2];

  void F(object[] xs) {
      var b = xs is
          [1, 2];
      var c = xs is
[1, 2];
      Use(xs is
          [1, 2]);
  }

  bool G(object[] xs) {
      return xs is
          [1, 2];
  }

  bool H(object xs) =>
      xs is
          { Length: 2 };
}
