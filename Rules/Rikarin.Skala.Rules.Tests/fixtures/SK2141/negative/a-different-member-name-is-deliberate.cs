// ⚠ The look-alike the whole rule is narrowed around. Raising a change notification for a
// property other than the one being set is exactly what the overload exists for, it is ordinary
// correct code, and it is the crosswise image of the defect: an explicit argument to a
// [CallerMemberName] parameter that is not what the compiler would have substituted.
//
// A rule that reports "an explicit argument overrides caller info" reports this, in every
// view model ever written. That is why only an exact restatement is a finding.
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Fixtures {
    sealed class Person : INotifyPropertyChanged {
        string first = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string First {
            get => first;
            set {
                first = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Display));
            }
        }

        public string Display => first;

        void OnPropertyChanged([CallerMemberName] string? member = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(member));
    }
}
