use crate::token::{Token, TokenKind};

// Lexer simple con soporte de comentarios tipo C/C++.
pub struct Lexer<'a> {
    src: &'a [u8],
    pos: usize,
    line: usize,
    col: usize,
}

impl<'a> Lexer<'a> {
    pub fn new(src: &'a str) -> Self {
        Self {
            src: src.as_bytes(),
            pos: 0,
            line: 1,
            col: 1,
        }
    }

    fn peek(&self) -> Option<u8> {
        self.src.get(self.pos).copied()
    }

    fn peek_next(&self) -> Option<u8> {
        self.src.get(self.pos + 1).copied()
    }

    fn bump(&mut self) -> Option<u8> {
        let ch = self.peek()?;
        self.pos += 1;
        if ch == b'\n' {
            self.line += 1;
            self.col = 1;
        } else {
            self.col += 1;
        }
        Some(ch)
    }

    fn skip_ws_and_comments(&mut self) {
        loop {
            while matches!(self.peek(), Some(b' ' | b'\t' | b'\r' | b'\n')) {
                self.bump();
            }

            // Comentario de línea: // ...
            if self.peek() == Some(b'/') && self.peek_next() == Some(b'/') {
                while let Some(ch) = self.bump() {
                    if ch == b'\n' {
                        break;
                    }
                }
                continue;
            }

            // Comentario de bloque: /* ... */
            if self.peek() == Some(b'/') && self.peek_next() == Some(b'*') {
                self.bump();
                self.bump();
                while let Some(ch) = self.bump() {
                    if ch == b'*' && self.peek() == Some(b'/') {
                        self.bump();
                        break;
                    }
                }
                continue;
            }

            break;
        }
    }

    fn read_ident_or_kw(&mut self) -> TokenKind {
        let start = self.pos;
        while matches!(
            self.peek(),
            Some(b'a'..=b'z' | b'A'..=b'Z' | b'_' | b'0'..=b'9')
        ) {
            self.bump();
        }
        let s = std::str::from_utf8(&self.src[start..self.pos]).unwrap();
        match s {
            "fn" => TokenKind::Fn,
            "struct" => TokenKind::Struct,
            "extern" => TokenKind::Extern,
            "import" => TokenKind::Import,
            "alias" => TokenKind::Alias,
            "return" => TokenKind::Return,
            "let" => TokenKind::Let,
            _ => TokenKind::Ident(s.to_string()),
        }
    }

    fn read_int(&mut self) -> TokenKind {
        let start = self.pos;
        while matches!(self.peek(), Some(b'0'..=b'9')) {
            self.bump();
        }
        let s = std::str::from_utf8(&self.src[start..self.pos]).unwrap();
        TokenKind::IntLit(s.parse().unwrap())
    }

    fn read_string(&mut self) -> Result<TokenKind, String> {
        self.bump(); // "
        let start = self.pos;
        while let Some(ch) = self.peek() {
            if ch == b'"' {
                let out = std::str::from_utf8(&self.src[start..self.pos])
                    .unwrap()
                    .to_string();
                self.bump();
                return Ok(TokenKind::StringLit(out));
            }
            self.bump();
        }
        Err("String sin cerrar".to_string())
    }

    pub fn tokenize(&mut self) -> Result<Vec<Token>, String> {
        let mut tokens = Vec::new();
        loop {
            self.skip_ws_and_comments();
            let line = self.line;
            let col = self.col;
            let kind = match self.peek() {
                None => TokenKind::Eof,
                Some(b'(') => {
                    self.bump();
                    TokenKind::LParen
                }
                Some(b')') => {
                    self.bump();
                    TokenKind::RParen
                }
                Some(b'{') => {
                    self.bump();
                    TokenKind::LBrace
                }
                Some(b'}') => {
                    self.bump();
                    TokenKind::RBrace
                }
                Some(b',') => {
                    self.bump();
                    TokenKind::Comma
                }
                Some(b';') => {
                    self.bump();
                    TokenKind::Semicolon
                }
                Some(b':') => {
                    self.bump();
                    TokenKind::Colon
                }
                Some(b'+') => {
                    self.bump();
                    TokenKind::Plus
                }
                Some(b'*') => {
                    self.bump();
                    TokenKind::Star
                }
                Some(b'/') => {
                    self.bump();
                    TokenKind::Slash
                }
                Some(b'=') => {
                    self.bump();
                    TokenKind::Equal
                }
                Some(b'-') => {
                    self.bump();
                    if self.peek() == Some(b'>') {
                        self.bump();
                        TokenKind::Arrow
                    } else {
                        TokenKind::Minus
                    }
                }
                Some(b'"') => self.read_string()?,
                Some(b'0'..=b'9') => self.read_int(),
                Some(b'a'..=b'z' | b'A'..=b'Z' | b'_') => self.read_ident_or_kw(),
                Some(other) => {
                    return Err(format!(
                        "Caracter no soportado '{}' en {}:{}",
                        other as char, line, col
                    ));
                }
            };
            tokens.push(Token::new(kind.clone(), line, col));
            if kind == TokenKind::Eof {
                break;
            }
        }
        Ok(tokens)
    }
}
