using System; class C { Action M(int value) => () => Console.WriteLine(value); }
