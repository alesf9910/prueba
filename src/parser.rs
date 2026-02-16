use crate::ast::*;
use crate::token::{Token, TokenKind};

pub struct Parser {
    tokens: Vec<Token>,
    pos: usize,
}

impl Parser {
    pub fn new(tokens: Vec<Token>) -> Self {
        Self { tokens, pos: 0 }
    }

    fn cur(&self) -> &Token {
        &self.tokens[self.pos]
    }

    fn bump(&mut self) {
        if self.pos < self.tokens.len() - 1 {
            self.pos += 1;
        }
    }

    fn eat(&mut self, kind: &TokenKind) -> bool {
        if &self.cur().kind == kind {
            self.bump();
            true
        } else {
            false
        }
    }

    fn expect(&mut self, kind: &TokenKind, msg: &str) -> Result<(), String> {
        if self.eat(kind) {
            Ok(())
        } else {
            Err(format!("{} en {}:{}", msg, self.cur().line, self.cur().col))
        }
    }

    fn expect_ident(&mut self, what: &str) -> Result<String, String> {
        match &self.cur().kind {
            TokenKind::Ident(s) => {
                let out = s.clone();
                self.bump();
                Ok(out)
            }
            _ => Err(format!(
                "Se esperaba {} en {}:{}",
                what,
                self.cur().line,
                self.cur().col
            )),
        }
    }

    fn parse_type(&mut self) -> Result<TypeName, String> {
        match &self.cur().kind {
            TokenKind::Ident(s) if s == "int" => {
                self.bump();
                Ok(TypeName::Int)
            }
            TokenKind::Ident(s) if s == "void" => {
                self.bump();
                Ok(TypeName::Void)
            }
            TokenKind::Ident(s) => {
                let n = s.clone();
                self.bump();
                Ok(TypeName::Named(n))
            }
            _ => Err(format!(
                "Tipo inválido en {}:{}",
                self.cur().line,
                self.cur().col
            )),
        }
    }

    pub fn parse_program(&mut self) -> Result<Program, String> {
        let mut imports = Vec::new();
        let mut aliases = Vec::new();
        let mut items = Vec::new();

        while self.cur().kind != TokenKind::Eof {
            match self.cur().kind.clone() {
                TokenKind::Import => imports.push(self.parse_import()?),
                TokenKind::Alias => aliases.push(self.parse_alias()?),
                _ => items.push(self.parse_item()?),
            }
        }

        Ok(Program {
            imports,
            aliases,
            items,
        })
    }

    fn parse_import(&mut self) -> Result<Import, String> {
        self.expect(&TokenKind::Import, "Falta 'import'")?;
        let path = match &self.cur().kind {
            TokenKind::StringLit(s) => {
                let p = s.clone();
                self.bump();
                p
            }
            _ => return Err("Se esperaba string en import".to_string()),
        };
        self.expect(&TokenKind::Semicolon, "Falta ';' en import")?;
        Ok(Import { path })
    }

    fn parse_alias(&mut self) -> Result<Alias, String> {
        self.expect(&TokenKind::Alias, "Falta 'alias'")?;
        let name = self.expect_ident("nombre de alias")?;
        self.expect(&TokenKind::Equal, "Falta '=' en alias")?;
        let path = match &self.cur().kind {
            TokenKind::StringLit(s) => {
                let p = s.clone();
                self.bump();
                p
            }
            _ => return Err("Se esperaba string en alias".to_string()),
        };
        self.expect(&TokenKind::Semicolon, "Falta ';' en alias")?;
        Ok(Alias { name, path })
    }

    fn parse_item(&mut self) -> Result<Item, String> {
        let is_extern = self.eat(&TokenKind::Extern);
        match self.cur().kind {
            TokenKind::Struct => Ok(Item::Struct(self.parse_struct(is_extern)?)),
            TokenKind::Fn => Ok(Item::Function(self.parse_function(is_extern)?)),
            _ => Err(format!(
                "Item inválido en {}:{}",
                self.cur().line,
                self.cur().col
            )),
        }
    }

    fn parse_struct(&mut self, is_extern: bool) -> Result<StructDecl, String> {
        self.expect(&TokenKind::Struct, "Falta 'struct'")?;
        let name = self.expect_ident("nombre de struct")?;
        self.expect(&TokenKind::LBrace, "Falta '{' en struct")?;

        let mut fields = Vec::new();
        while self.cur().kind != TokenKind::RBrace {
            let ty = self.parse_type()?;
            let field_name = self.expect_ident("nombre de campo")?;
            self.expect(&TokenKind::Semicolon, "Falta ';' en campo")?;
            fields.push(FieldDecl {
                ty,
                name: field_name,
            });
        }
        self.expect(&TokenKind::RBrace, "Falta '}' en struct")?;
        self.expect(&TokenKind::Semicolon, "Falta ';' tras struct")?;

        Ok(StructDecl {
            name,
            fields,
            is_extern,
        })
    }

    fn parse_function(&mut self, is_extern: bool) -> Result<FunctionDecl, String> {
        self.expect(&TokenKind::Fn, "Falta 'fn'")?;
        let name = self.expect_ident("nombre de función")?;
        self.expect(&TokenKind::LParen, "Falta '('")?;

        let mut params = Vec::new();
        if self.cur().kind != TokenKind::RParen {
            loop {
                let ty = self.parse_type()?;
                let pname = self.expect_ident("nombre de parámetro")?;
                params.push(ParamDecl { ty, name: pname });
                if !self.eat(&TokenKind::Comma) {
                    break;
                }
            }
        }
        self.expect(&TokenKind::RParen, "Falta ')' en función")?;
        self.expect(&TokenKind::Arrow, "Falta '->' en función")?;
        let ret_ty = self.parse_type()?;

        if self.eat(&TokenKind::Semicolon) {
            return Ok(FunctionDecl {
                name,
                params,
                ret_ty,
                body: None,
                is_extern: true,
            });
        }

        self.expect(&TokenKind::LBrace, "Falta '{' en cuerpo")?;
        let mut body = Vec::new();
        while self.cur().kind != TokenKind::RBrace {
            body.push(self.parse_stmt()?);
        }
        self.expect(&TokenKind::RBrace, "Falta '}' en cuerpo")?;

        Ok(FunctionDecl {
            name,
            params,
            ret_ty,
            body: Some(body),
            is_extern,
        })
    }

    fn parse_stmt(&mut self) -> Result<Stmt, String> {
        match self.cur().kind {
            TokenKind::Let => {
                self.bump();
                let ty = self.parse_type()?;
                let name = self.expect_ident("nombre de variable")?;
                self.expect(&TokenKind::Equal, "Falta '=' en let")?;
                let value = self.parse_expr()?;
                self.expect(&TokenKind::Semicolon, "Falta ';' en let")?;
                Ok(Stmt::Let { ty, name, value })
            }
            TokenKind::Return => {
                self.bump();
                let e = self.parse_expr()?;
                self.expect(&TokenKind::Semicolon, "Falta ';' en return")?;
                Ok(Stmt::Return(e))
            }
            _ => {
                let e = self.parse_expr()?;
                self.expect(&TokenKind::Semicolon, "Falta ';' en expresión")?;
                Ok(Stmt::Expr(e))
            }
        }
    }

    fn parse_expr(&mut self) -> Result<Expr, String> {
        self.parse_add_sub()
    }

    fn parse_add_sub(&mut self) -> Result<Expr, String> {
        let mut node = self.parse_mul_div()?;
        loop {
            let op = match self.cur().kind {
                TokenKind::Plus => BinOp::Add,
                TokenKind::Minus => BinOp::Sub,
                _ => break,
            };
            self.bump();
            let rhs = self.parse_mul_div()?;
            node = Expr::Binary {
                op,
                lhs: Box::new(node),
                rhs: Box::new(rhs),
            };
        }
        Ok(node)
    }

    fn parse_mul_div(&mut self) -> Result<Expr, String> {
        let mut node = self.parse_primary()?;
        loop {
            let op = match self.cur().kind {
                TokenKind::Star => BinOp::Mul,
                TokenKind::Slash => BinOp::Div,
                _ => break,
            };
            self.bump();
            let rhs = self.parse_primary()?;
            node = Expr::Binary {
                op,
                lhs: Box::new(node),
                rhs: Box::new(rhs),
            };
        }
        Ok(node)
    }

    fn parse_primary(&mut self) -> Result<Expr, String> {
        match self.cur().kind.clone() {
            TokenKind::IntLit(n) => {
                self.bump();
                Ok(Expr::Int(n))
            }
            TokenKind::Ident(name) => {
                self.bump();
                if self.eat(&TokenKind::LParen) {
                    let mut args = Vec::new();
                    if self.cur().kind != TokenKind::RParen {
                        loop {
                            args.push(self.parse_expr()?);
                            if !self.eat(&TokenKind::Comma) {
                                break;
                            }
                        }
                    }
                    self.expect(&TokenKind::RParen, "Falta ')' en llamada")?;
                    Ok(Expr::Call { name, args })
                } else {
                    Ok(Expr::Var(name))
                }
            }
            TokenKind::LParen => {
                self.bump();
                let expr = self.parse_expr()?;
                self.expect(&TokenKind::RParen, "Falta ')' agrupación")?;
                Ok(expr)
            }
            _ => Err(format!(
                "Expresión inválida en {}:{}",
                self.cur().line,
                self.cur().col
            )),
        }
    }
}
