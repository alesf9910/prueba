namespace Prueba;

public sealed class Lexer(string source)
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

            if (char.IsLetter(current) || current == '_')
            {
                tokens.Add(ReadIdentifierOrKeyword());
                continue;
            }

            if (char.IsDigit(current))
            {
                tokens.Add(ReadNumber());
                continue;
            }

            tokens.Add(ReadSymbol());
        }

        tokens.Add(new Token(TokenKind.EndOfInput, string.Empty, _position));
        return tokens;
    }

    private Token ReadIdentifierOrKeyword()
    {
        var start = _position;
        while (!IsAtEnd() && (char.IsLetterOrDigit(Peek()) || Peek() == '_'))
        {
            Advance();
        }

        var text = source[start.._position];
        if (Token.Keywords.TryGetValue(text, out var kind))
        {
            return new Token(kind, text, start);
        }

        return new Token(TokenKind.Identifier, text, start);
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
        if (hasDot)
        {
            if (!double.TryParse(lexeme, out var f64))
            {
                throw new FormatException($"Invalid floating literal '{lexeme}' at position {start}.");
            }

            return new Token(TokenKind.F64Literal, lexeme, start, f64);
        }

        if (!long.TryParse(lexeme, out var i64))
        {
            throw new FormatException($"Invalid integer literal '{lexeme}' at position {start}.");
        }

        return new Token(TokenKind.I64Literal, lexeme, start, i64);
    }

    private Token ReadSymbol()
    {
        var position = _position;
        var current = Advance();

        return current switch
        {
            '(' => new Token(TokenKind.OpenParen, "(", position),
            ')' => new Token(TokenKind.CloseParen, ")", position),
            '{' => new Token(TokenKind.OpenBrace, "{", position),
            '}' => new Token(TokenKind.CloseBrace, "}", position),
            ',' => new Token(TokenKind.Comma, ",", position),
            ':' => new Token(TokenKind.Colon, ":", position),
            ';' => new Token(TokenKind.Semicolon, ";", position),
            '+' => new Token(TokenKind.Plus, "+", position),
            '-' => new Token(TokenKind.Minus, "-", position),
            '*' => new Token(TokenKind.Star, "*", position),
            '/' => new Token(TokenKind.Slash, "/", position),
            '%' => new Token(TokenKind.Percent, "%", position),
            '=' => Match('=')
                ? new Token(TokenKind.EqualEqual, "==", position)
                : new Token(TokenKind.Equal, "=", position),
            '!' => Match('=')
                ? new Token(TokenKind.BangEqual, "!=", position)
                : new Token(TokenKind.Bang, "!", position),
            '<' => Match('=')
                ? new Token(TokenKind.LessEqual, "<=", position)
                : new Token(TokenKind.Less, "<", position),
            '>' => Match('=')
                ? new Token(TokenKind.GreaterEqual, ">=", position)
                : new Token(TokenKind.Greater, ">", position),
            '&' when Match('&') => new Token(TokenKind.AndAnd, "&&", position),
            '|' when Match('|') => new Token(TokenKind.OrOr, "||", position),
            _ => throw new InvalidOperationException($"Unexpected character '{current}' at position {position}.")
        };
    }

    private bool Match(char expected)
    {
        if (IsAtEnd() || source[_position] != expected)
        {
            return false;
        }

        _position++;
        return true;
    }

    private char Advance() => source[_position++];

    private char Peek() => source[_position];

    private bool IsAtEnd() => _position >= source.Length;
}

public sealed record Token(TokenKind Kind, string Lexeme, int Position, object? Literal = null)
{
    public static readonly IReadOnlyDictionary<string, TokenKind> Keywords = new Dictionary<string, TokenKind>(StringComparer.Ordinal)
    {
        ["fn"] = TokenKind.Fn,
        ["let"] = TokenKind.Let,
        ["return"] = TokenKind.Return,
        ["if"] = TokenKind.If,
        ["else"] = TokenKind.Else,
        ["while"] = TokenKind.While,
        ["true"] = TokenKind.True,
        ["false"] = TokenKind.False,
        ["i32"] = TokenKind.TypeI32,
        ["i64"] = TokenKind.TypeI64,
        ["f64"] = TokenKind.TypeF64,
        ["bool"] = TokenKind.TypeBool,
        ["ptr"] = TokenKind.TypePtr,
        ["void"] = TokenKind.TypeVoid
    };
}

public enum TokenKind
{
    Identifier,
    I64Literal,
    F64Literal,
    True,
    False,
    Fn,
    Let,
    Return,
    If,
    Else,
    While,
    TypeI32,
    TypeI64,
    TypeF64,
    TypeBool,
    TypePtr,
    TypeVoid,
    OpenParen,
    CloseParen,
    OpenBrace,
    CloseBrace,
    Comma,
    Colon,
    Semicolon,
    Plus,
    Minus,
    Star,
    Slash,
    Percent,
    Bang,
    Equal,
    Less,
    Greater,
    EqualEqual,
    BangEqual,
    LessEqual,
    GreaterEqual,
    AndAnd,
    OrOr,
    EndOfInput
}
