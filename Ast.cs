namespace Prueba;

public abstract record Node
{
    public sealed record Program(IReadOnlyList<Function> Functions) : Node;

    public sealed record Function(
        string Name,
        IReadOnlyList<Parameter> Parameters,
        TypeSymbol ReturnType,
        Block Body) : Node;

    public sealed record Parameter(string Name, TypeSymbol Type) : Node;

    public sealed record Block(IReadOnlyList<Statement> Statements) : Node;

    public abstract record Statement : Node
    {
        public sealed record VariableDeclaration(string Name, TypeSymbol Type, Expression? Initializer) : Statement;
        public sealed record Assignment(string Name, Expression Value) : Statement;
        public sealed record Return(Expression? Value) : Statement;
        public sealed record ExpressionStatement(Expression Expression) : Statement;
        public sealed record If(Expression Condition, Block Then, Block? Else) : Statement;
        public sealed record While(Expression Condition, Block Body) : Statement;
    }

    public abstract record Expression : Node
    {
        public sealed record Literal(object? Value, TypeSymbol Type) : Expression;
        public sealed record Variable(string Name) : Expression;
        public sealed record Binary(Expression Left, TokenKind Operator, Expression Right) : Expression;
        public sealed record Unary(TokenKind Operator, Expression Operand) : Expression;
        public sealed record Call(string Callee, IReadOnlyList<Expression> Arguments) : Expression;
    }
}

public enum TypeSymbol
{
    Void,
    I32,
    I64,
    F64,
    Bool,
    Ptr
}
