using System.Text;

namespace NeoC.Core;

// Lexer como ref struct para estado mutable eficiente (sin clases de dominio).
public ref struct Lexer
{
    private readonly string _source;
    private int _i;
    private int _line;
    private int _col;

    public Lexer(string source)
    {
        _source = source;
        _i = 0;
        _line = 1;
        _col = 1;
    }

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();
        while (true)
        {
            SkipWhitespaceAndComments();
            var line = _line;
            var col = _col;
            var ch = Peek();
            if (ch is null)
            {
                tokens.Add(new(TokenKind.Eof, string.Empty, line, col));
                break;
            }

            Token token = ch switch
            {
                '(' => Make(TokenKind.LParen, "(", line, col, 1),
                ')' => Make(TokenKind.RParen, ")", line, col, 1),
                '{' => Make(TokenKind.LBrace, "{", line, col, 1),
                '}' => Make(TokenKind.RBrace, "}", line, col, 1),
                ',' => Make(TokenKind.Comma, ",", line, col, 1),
                ';' => Make(TokenKind.Semicolon, ";", line, col, 1),
                '+' => Make(TokenKind.Plus, "+", line, col, 1),
                '*' => Make(TokenKind.Star, "*", line, col, 1),
                '/' => Make(TokenKind.Slash, "/", line, col, 1),
                '=' => Make(TokenKind.Equal, "=", line, col, 1),
                '-' when Peek(1) == '>' => Make(TokenKind.Arrow, "->", line, col, 2),
                '-' => Make(TokenKind.Minus, "-", line, col, 1),
                '"' => ReadString(),
                _ when char.IsAsciiDigit(ch.Value) => ReadInt(),
                _ when char.IsAsciiLetter(ch.Value) || ch == '_' => ReadIdentOrKeyword(),
                _ => throw new Exception($"Carácter no soportado '{ch}' en {line}:{col}")
            };

            tokens.Add(token);
        }

        return tokens;
    }

    private void SkipWhitespaceAndComments()
    {
        while (true)
        {
            while (Peek() is char ch && char.IsWhiteSpace(ch)) { _ = Advance(); }

            if (Peek() == '/' && Peek(1) == '/')
            {
                while (Peek() is char c && c != '\n') { _ = Advance(); }
                continue;
            }

            if (Peek() == '/' && Peek(1) == '*')
            {
                _ = Advance();
                _ = Advance();
                while (Peek() is char)
                {
                    if (Peek() == '*' && Peek(1) == '/')
                    {
                        _ = Advance();
                        _ = Advance();
                        break;
                    }
                    _ = Advance();
                }
                continue;
            }

            break;
        }
    }

    private Token ReadIdentOrKeyword()
    {
        var line = _line;
        var col = _col;
        var sb = new StringBuilder();

        while (Peek() is char ch && (char.IsAsciiLetterOrDigit(ch) || ch == '_'))
        {
            sb.Append(Advance());
        }

        var text = sb.ToString();
        var kind = text switch
        {
            "fn" => TokenKind.Fn,
            "struct" => TokenKind.Struct,
            "extern" => TokenKind.Extern,
            "import" => TokenKind.Import,
            "alias" => TokenKind.Alias,
            "return" => TokenKind.Return,
            "let" => TokenKind.Let,
            _ => TokenKind.Ident
        };

        return new(kind, text, line, col);
    }

    private Token ReadInt()
    {
        var line = _line;
        var col = _col;
        var sb = new StringBuilder();

        while (Peek() is char ch && char.IsAsciiDigit(ch))
        {
            sb.Append(Advance());
        }

        return new(TokenKind.IntLit, sb.ToString(), line, col);
    }

    private Token ReadString()
    {
        var line = _line;
        var col = _col;
        _ = Advance();

        var sb = new StringBuilder();
        while (Peek() is char ch)
        {
            if (ch == '"')
            {
                _ = Advance();
                return new(TokenKind.StringLit, sb.ToString(), line, col);
            }
            sb.Append(Advance());
        }

        throw new Exception($"String sin cerrar en {line}:{col}");
    }

    private Token Make(TokenKind kind, string lexeme, int line, int col, int len)
    {
        for (var k = 0; k < len; k++) { _ = Advance(); }
        return new(kind, lexeme, line, col);
    }

    private char? Peek(int look = 0) => _i + look < _source.Length ? _source[_i + look] : null;

    private char Advance()
    {
        var ch = _source[_i++];
        if (ch == '\n')
        {
            _line++;
            _col = 1;
        }
        else
        {
            _col++;
        }
        return ch;
    }
}
