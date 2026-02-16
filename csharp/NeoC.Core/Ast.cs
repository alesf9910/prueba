namespace NeoC.Core;

public readonly record struct ProgramNode(
    IReadOnlyList<ImportNode> Imports,
    IReadOnlyList<AliasNode> Aliases,
    IReadOnlyList<ItemNode> Items
);

public readonly record struct ImportNode(string Path);
public readonly record struct AliasNode(string Name, string Path);

public enum ItemKind { Struct, Function }
public readonly record struct ItemNode(ItemKind Kind, StructNode Struct, FunctionNode Function)
{
    public static ItemNode FromStruct(StructNode value) => new(ItemKind.Struct, value, default);
    public static ItemNode FromFunction(FunctionNode value) => new(ItemKind.Function, default, value);
}

public readonly record struct StructNode(string Name, IReadOnlyList<FieldNode> Fields, bool IsExtern);
public readonly record struct FieldNode(TypeRef Type, string Name);

public readonly record struct FunctionNode(
    string Name,
    IReadOnlyList<ParamNode> Parameters,
    TypeRef ReturnType,
    IReadOnlyList<StmtNode>? Body,
    bool IsExtern
);

public readonly record struct ParamNode(TypeRef Type, string Name);

public enum TypeKind { Int, Void, Named }
public readonly record struct TypeRef(TypeKind Kind, string Name)
{
    public static TypeRef Int => new(TypeKind.Int, "int");
    public static TypeRef Void => new(TypeKind.Void, "void");
    public static TypeRef Named(string name) => new(TypeKind.Named, name);
}

public enum StmtKind { Let, Return, Expr }
public readonly record struct StmtNode(StmtKind Kind, LetStmt Let, ReturnStmt Return, ExprStmt Expr)
{
    public static StmtNode FromLet(LetStmt value) => new(StmtKind.Let, value, default, default);
    public static StmtNode FromReturn(ReturnStmt value) => new(StmtKind.Return, default, value, default);
    public static StmtNode FromExpr(ExprStmt value) => new(StmtKind.Expr, default, default, value);
}

public readonly record struct LetStmt(TypeRef Type, string Name, ExprNode Value);
public readonly record struct ReturnStmt(ExprNode Value);
public readonly record struct ExprStmt(ExprNode Value);

public enum ExprKind { Int, Var, Call, Binary }
public readonly record struct ExprNode(ExprKind Kind, IntExpr Int, VarExpr Var, CallExpr Call, BinaryExpr Binary)
{
    public static ExprNode FromInt(IntExpr value) => new(ExprKind.Int, value, default, default, default);
    public static ExprNode FromVar(VarExpr value) => new(ExprKind.Var, default, value, default, default);
    public static ExprNode FromCall(CallExpr value) => new(ExprKind.Call, default, default, value, default);
    public static ExprNode FromBinary(BinaryExpr value) => new(ExprKind.Binary, default, default, default, value);
}

public readonly record struct IntExpr(long Value);
public readonly record struct VarExpr(string Name);
public readonly record struct CallExpr(string Name, IReadOnlyList<ExprNode> Args);
public readonly record struct BinaryExpr(BinaryOp Op, ExprNode Left, ExprNode Right);

public enum BinaryOp { Add, Sub, Mul, Div }
