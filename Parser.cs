namespace Prueba;

public sealed class Parser(IReadOnlyList<Token> tokens)
{
    private int _current;

    public Node.Program ParseProgram()
    {
        var functions = new List<Node.Function>();
        while (!IsAtEnd())
        {
            functions.Add(ParseFunction());
        }

        return new Node.Program(functions);
    }

    private Node.Function ParseFunction()
    {
        Consume(TokenKind.Fn, "Expected 'fn' at start of function.");
        var name = Consume(TokenKind.Identifier, "Expected function name.").Lexeme;
        Consume(TokenKind.OpenParen, "Expected '('.");

        var parameters = new List<Node.Parameter>();
        if (!Check(TokenKind.CloseParen))
        {
            do
            {
                var paramName = Consume(TokenKind.Identifier, "Expected parameter name.").Lexeme;
                Consume(TokenKind.Colon, "Expected ':' after parameter name.");
                var paramType = ParseType();
                parameters.Add(new Node.Parameter(paramName, paramType));
            } while (Match(TokenKind.Comma));
        }

        Consume(TokenKind.CloseParen, "Expected ')'.");
        Consume(TokenKind.Colon, "Expected ':' before return type.");
        var returnType = ParseType();
        var body = ParseBlock();

        return new Node.Function(name, parameters, returnType, body);
    }

    private Node.Block ParseBlock()
    {
        Consume(TokenKind.OpenBrace, "Expected '{' to start block.");
        var statements = new List<Node.Statement>();
        while (!Check(TokenKind.CloseBrace) && !IsAtEnd())
        {
            statements.Add(ParseStatement());
        }

        Consume(TokenKind.CloseBrace, "Expected '}' to close block.");
        return new Node.Block(statements);
    }

    private Node.Statement ParseStatement()
    {
        if (Match(TokenKind.Let))
        {
            var name = Consume(TokenKind.Identifier, "Expected variable name.").Lexeme;
            Consume(TokenKind.Colon, "Expected ':' after variable name.");
            var type = ParseType();
            Node.Expression? initializer = null;
            if (Match(TokenKind.Equal))
            {
                initializer = ParseExpression();
            }

            Consume(TokenKind.Semicolon, "Expected ';' after declaration.");
            return new Node.Statement.VariableDeclaration(name, type, initializer);
        }

        if (Match(TokenKind.Return))
        {
            Node.Expression? value = null;
            if (!Check(TokenKind.Semicolon))
            {
                value = ParseExpression();
            }

            Consume(TokenKind.Semicolon, "Expected ';' after return.");
            return new Node.Statement.Return(value);
        }

        if (Match(TokenKind.If))
        {
            Consume(TokenKind.OpenParen, "Expected '(' after if.");
            var condition = ParseExpression();
            Consume(TokenKind.CloseParen, "Expected ')' after if condition.");
            var thenBlock = ParseBlock();
            Node.Block? elseBlock = null;
            if (Match(TokenKind.Else))
            {
                elseBlock = ParseBlock();
            }

            return new Node.Statement.If(condition, thenBlock, elseBlock);
        }

        if (Match(TokenKind.While))
        {
            Consume(TokenKind.OpenParen, "Expected '(' after while.");
            var condition = ParseExpression();
            Consume(TokenKind.CloseParen, "Expected ')' after while condition.");
            var body = ParseBlock();
            return new Node.Statement.While(condition, body);
        }

        if (Check(TokenKind.Identifier) && PeekNext().Kind == TokenKind.Equal)
        {
            var name = Consume(TokenKind.Identifier, "Expected variable name.").Lexeme;
            Consume(TokenKind.Equal, "Expected '=' for assignment.");
            var value = ParseExpression();
            Consume(TokenKind.Semicolon, "Expected ';' after assignment.");
            return new Node.Statement.Assignment(name, value);
        }

        var expr = ParseExpression();
        Consume(TokenKind.Semicolon, "Expected ';' after expression.");
        return new Node.Statement.ExpressionStatement(expr);
    }

    private Node.Expression ParseExpression() => ParseLogicalOr();

    private Node.Expression ParseLogicalOr()
    {
        var expr = ParseLogicalAnd();
        while (Match(TokenKind.OrOr))
        {
            var oper = Previous();
            var right = ParseLogicalAnd();
            expr = new Node.Expression.Binary(expr, oper.Kind, right);
        }

        return expr;
    }

    private Node.Expression ParseLogicalAnd()
    {
        var expr = ParseEquality();
        while (Match(TokenKind.AndAnd))
        {
            var oper = Previous();
            var right = ParseEquality();
            expr = new Node.Expression.Binary(expr, oper.Kind, right);
        }

        return expr;
    }

    private Node.Expression ParseEquality()
    {
        var expr = ParseComparison();
        while (Match(TokenKind.EqualEqual, TokenKind.BangEqual))
        {
            var oper = Previous();
            var right = ParseComparison();
            expr = new Node.Expression.Binary(expr, oper.Kind, right);
        }

        return expr;
    }

    private Node.Expression ParseComparison()
    {
        var expr = ParseTerm();
        while (Match(TokenKind.Less, TokenKind.LessEqual, TokenKind.Greater, TokenKind.GreaterEqual))
        {
            var oper = Previous();
            var right = ParseTerm();
            expr = new Node.Expression.Binary(expr, oper.Kind, right);
        }

        return expr;
    }

    private Node.Expression ParseTerm()
    {
        var expr = ParseFactor();
        while (Match(TokenKind.Plus, TokenKind.Minus))
        {
            var oper = Previous();
            var right = ParseFactor();
            expr = new Node.Expression.Binary(expr, oper.Kind, right);
        }

        return expr;
    }

    private Node.Expression ParseFactor()
    {
        var expr = ParseUnary();
        while (Match(TokenKind.Star, TokenKind.Slash, TokenKind.Percent))
        {
            var oper = Previous();
            var right = ParseUnary();
            expr = new Node.Expression.Binary(expr, oper.Kind, right);
        }

        return expr;
    }

    private Node.Expression ParseUnary()
    {
        if (Match(TokenKind.Bang, TokenKind.Minus))
        {
            var oper = Previous();
            var operand = ParseUnary();
            return new Node.Expression.Unary(oper.Kind, operand);
        }

        return ParseCall();
    }

    private Node.Expression ParseCall()
    {
        var expr = ParsePrimary();
        if (expr is Node.Expression.Variable variable && Match(TokenKind.OpenParen))
        {
            var args = new List<Node.Expression>();
            if (!Check(TokenKind.CloseParen))
            {
                do
                {
                    args.Add(ParseExpression());
                } while (Match(TokenKind.Comma));
            }

            Consume(TokenKind.CloseParen, "Expected ')' after arguments.");
            return new Node.Expression.Call(variable.Name, args);
        }

        return expr;
    }

    private Node.Expression ParsePrimary()
    {
        if (Match(TokenKind.I64Literal))
        {
            var literal = Previous();
            return new Node.Expression.Literal(literal.Literal, TypeSymbol.I64);
        }

        if (Match(TokenKind.F64Literal))
        {
            var literal = Previous();
            return new Node.Expression.Literal(literal.Literal, TypeSymbol.F64);
        }

        if (Match(TokenKind.True))
        {
            return new Node.Expression.Literal(true, TypeSymbol.Bool);
        }

        if (Match(TokenKind.False))
        {
            return new Node.Expression.Literal(false, TypeSymbol.Bool);
        }

        if (Match(TokenKind.Identifier))
        {
            return new Node.Expression.Variable(Previous().Lexeme);
        }

        if (Match(TokenKind.OpenParen))
        {
            var expr = ParseExpression();
            Consume(TokenKind.CloseParen, "Expected ')' after expression.");
            return expr;
        }

        throw new InvalidOperationException($"Unexpected token '{Peek().Kind}' at position {Peek().Position}.");
    }

    private TypeSymbol ParseType()
    {
        if (Match(TokenKind.TypeI32))
        {
            return TypeSymbol.I32;
        }

        if (Match(TokenKind.TypeI64))
        {
            return TypeSymbol.I64;
        }

        if (Match(TokenKind.TypeF64))
        {
            return TypeSymbol.F64;
        }

        if (Match(TokenKind.TypeBool))
        {
            return TypeSymbol.Bool;
        }

        if (Match(TokenKind.TypePtr))
        {
            return TypeSymbol.Ptr;
        }

        if (Match(TokenKind.TypeVoid))
        {
            return TypeSymbol.Void;
        }

        throw new InvalidOperationException($"Expected type at position {Peek().Position}.");
    }

    private bool Match(params TokenKind[] kinds)
    {
        foreach (var kind in kinds)
        {
            if (Check(kind))
            {
                Advance();
                return true;
            }
        }

        return false;
    }

    private Token Consume(TokenKind kind, string message)
    {
        if (Check(kind))
        {
            return Advance();
        }

        throw new InvalidOperationException(message);
    }

    private bool Check(TokenKind kind) => !IsAtEnd() && Peek().Kind == kind;

    private Token Advance()
    {
        if (!IsAtEnd())
        {
            _current++;
        }

        return Previous();
    }

    private bool IsAtEnd() => Peek().Kind == TokenKind.EndOfInput;

    private Token Peek() => tokens[_current];

    private Token PeekNext() => _current + 1 < tokens.Count ? tokens[_current + 1] : tokens[^1];

    private Token Previous() => tokens[_current - 1];
}
