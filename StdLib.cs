namespace Prueba;

public static class StdLib
{
    public static readonly IReadOnlyList<NativeFunction> Functions =
    [
        new NativeFunction("mem_alloc", TypeSymbol.Ptr, [new NativeParameter("size", TypeSymbol.I64)]),
        new NativeFunction("mem_free", TypeSymbol.Void, [new NativeParameter("ptr", TypeSymbol.Ptr)]),
        new NativeFunction("mem_copy", TypeSymbol.Void, [new NativeParameter("dest", TypeSymbol.Ptr), new NativeParameter("src", TypeSymbol.Ptr), new NativeParameter("size", TypeSymbol.I64)]),
        new NativeFunction("write", TypeSymbol.I64, [new NativeParameter("fd", TypeSymbol.I32), new NativeParameter("buffer", TypeSymbol.Ptr), new NativeParameter("len", TypeSymbol.I64)]),
        new NativeFunction("putchar", TypeSymbol.I32, [new NativeParameter("value", TypeSymbol.I32)])
    ];

    public sealed record NativeFunction(string Name, TypeSymbol ReturnType, IReadOnlyList<NativeParameter> Parameters);

    public sealed record NativeParameter(string Name, TypeSymbol Type);
}
