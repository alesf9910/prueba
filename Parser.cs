namespace Prueba;

public sealed class Parser
{
    public sealed record ParseResult(Expression Root, IReadOnlyList<Token> Tokens);

    public ParseResult Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var lexer = new Lexer(source);
        var tokens = lexer.Lex();
        var parser = new ExpressionParser(tokens);
        var root = parser.ParseExpression();
        return new ParseResult(root, tokens);
    }

    public bool TryParse(string source, out ParseResult? result)
    {
        try
        {
            result = Parse(source);
            return true;
        }
        catch (Exception)
        {
            result = null;
            return false;
        }
    }

    public abstract record Expression
    {
        public sealed record Number(decimal Value) : Expression;
        public sealed record Binary(Expression Left, TokenKind Operator, Expression Right) : Expression;
        public sealed record Unary(TokenKind Operator, Expression Operand) : Expression;
    }

    public sealed record Token(TokenKind Kind, string Lexeme, int Position, decimal? NumberValue = null);

    public enum TokenKind
    {
        Number,
        Plus,
        Minus,
        Star,
        Slash,
        OpenParen,
        CloseParen,
        EndOfInput
    }

    private sealed class Lexer(string source)
    {
        private int _position;

        public IReadOnlyList<Token> Lex()
        {
            var tokens = new List<Token>();

            while (!IsAtEnd())
            {
                var current = Peek();
                if (char.IsWhiteSpace(current))
                {
                    Advance();
                    continue;
                }

                if (char.IsDigit(current) || current == '.')
                {
                    tokens.Add(ReadNumber());
                    continue;
                }

                tokens.Add(ReadSymbol());
            }

            tokens.Add(new Token(TokenKind.EndOfInput, string.Empty, _position));
            return tokens;
        }

        private Token ReadNumber()
        {
            var start = _position;
            var hasDot = false;

            while (!IsAtEnd())
            {
                var current = Peek();
                if (current == '.')
                {
                    if (hasDot)
                    {
                        break;
                    }

                    hasDot = true;
                    Advance();
                    continue;
                }

                if (!char.IsDigit(current))
                {
                    break;
                }

                Advance();
            }

            var lexeme = source[start.._position];
            if (!decimal.TryParse(lexeme, out var value))
            {
                throw new FormatException($"Invalid number literal '{lexeme}' at position {start}.");
            }

            return new Token(TokenKind.Number, lexeme, start, value);
        }

        private Token ReadSymbol()
        {
            var position = _position;
            var current = Advance();

            return current switch
            {
                '+' => new Token(TokenKind.Plus, "+", position),
                '-' => new Token(TokenKind.Minus, "-", position),
                '*' => new Token(TokenKind.Star, "*", position),
                '/' => new Token(TokenKind.Slash, "/", position),
                '(' => new Token(TokenKind.OpenParen, "(", position),
                ')' => new Token(TokenKind.CloseParen, ")", position),
                _ => throw new InvalidOperationException($"Unexpected character '{current}' at position {position}.")
            };
        }

        private char Advance() => source[_position++];

        private char Peek() => source[_position];

        private bool IsAtEnd() => _position >= source.Length;
    }

    private sealed class ExpressionParser(IReadOnlyList<Token> tokens)
    {
        private int _current;

        public Expression ParseExpression()
        {
            var expression = ParseTerm();

            while (Match(TokenKind.Plus, TokenKind.Minus))
            {
                var oper = Previous();
                var right = ParseTerm();
                expression = new Expression.Binary(expression, oper.Kind, right);
            }

            return expression;
        }

        private Expression ParseTerm()
        {
            var expression = ParseFactor();

            while (Match(TokenKind.Star, TokenKind.Slash))
            {
                var oper = Previous();
                var right = ParseFactor();
                expression = new Expression.Binary(expression, oper.Kind, right);
            }

            return expression;
        }

        private Expression ParseFactor()
        {
            if (Match(TokenKind.Minus))
            {
                var oper = Previous();
                var right = ParseFactor();
                return new Expression.Unary(oper.Kind, right);
            }

            if (Match(TokenKind.Number))
            {
                var number = Previous();
                return new Expression.Number(number.NumberValue ?? 0m);
            }

            if (Match(TokenKind.OpenParen))
            {
                var expression = ParseExpression();
                Consume(TokenKind.CloseParen, "Expected ')' after expression.");
                return expression;
            }

            throw new InvalidOperationException($"Unexpected token '{Peek().Kind}' at position {Peek().Position}.");
        }

        private Token Consume(TokenKind kind, string message)
        {
            if (Check(kind))
            {
                return Advance();
            }

            throw new InvalidOperationException(message);
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

        private bool Check(TokenKind kind)
        {
            if (IsAtEnd())
            {
                return false;
            }

            return Peek().Kind == kind;
        }

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

        private Token Previous() => tokens[_current - 1];
    }
}
