using System.Threading.Tasks;

// ⚠ The lambda names only a parameter and a *static* method, so it captures no `this` and the pool
// thread cannot see this object at all. Starting work from a constructor is not by itself the
// finding, and a rule that said so would be a complaint about `Task.Run`. `Compute` is deliberately
// static: were it an instance method the closure would carry `this` and this file would be a
// positive fixture wearing a negative's comment.
public sealed class Sizer {
    public Sizer(int size) {
        Task.Run(() => Compute(size));
    }

    static int Compute(int size) => size * 2;
}
