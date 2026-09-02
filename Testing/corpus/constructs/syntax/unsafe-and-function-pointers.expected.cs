// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaFormatOnly generated=2026-09-02
using System;

// UnsafeStatement, FunctionPointerCallingConvention, FunctionPointerUnmanagedCallingConvention and
// FunctionPointerUnmanagedCallingConventionList occurred nowhere; FunctionPointerType and
// FunctionPointerParameterList occurred once, in a pathological file. The calling-convention list is
// a bracketed list inside an angle-bracketed one, which is a nesting nothing else in the corpus has.
unsafe class UnsafeAndFunctionPointers {
    delegate* managed<int, int> Managed;

    delegate* unmanaged<int, byte*, void> Unmanaged;

    delegate* unmanaged[Cdecl]<int, int> OneConvention;

    delegate* unmanaged[Cdecl, SuppressGCTransition]<int, byte*, nint, double, void> TwoConventions;

    delegate* unmanaged[Stdcall, MemberFunction, SuppressGCTransition]<int, byte*, nint, double, long, void>
        Overflowing;

    static int Invoke(delegate* managed<int, int> callback, int subject) => callback(subject);

    static void Statements(int* buffer, int length) {
        unsafe {
            for (var index = 0; index < length; index++) {
                buffer[index] = index;
            }
        }

        unsafe {
            var head = buffer;
            var tail = buffer + length;
            while (head != tail) {
                *head++ = 0;
            }
        }
    }

    struct Node {
        public int Value;
        public Node* Next;
    }

    // PointerMemberAccessExpression occurred once. A `->` chain long enough to wrap is what
    // `resharper_csharp_space_around_arrow_op` and the chained-member-access rules disagree about.
    static int Chained(Node* node) =>
        node->Next->Next->Next->Value + node->Next->Value + node->Value + node->Next->Next->Value;
}
