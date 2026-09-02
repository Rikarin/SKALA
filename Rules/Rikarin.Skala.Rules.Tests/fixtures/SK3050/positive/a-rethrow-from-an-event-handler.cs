using System;
using System.IO;
using System.Threading.Tasks;

// ⚠ The legitimate `async void` — an event handler — is exactly where this is fatal. The `catch`
// caught the exception, wrote it down and then handed it to the synchronization context, which is
// strictly worse than having written no handler at all.
public sealed class Editor {
    public async void OnSaveClicked(object sender, EventArgs e) {
        try {
            await SaveAsync();
        } catch (IOException) {
            Console.WriteLine("save failed");
            throw;
        }
    }

    static Task SaveAsync() => Task.CompletedTask;
}
