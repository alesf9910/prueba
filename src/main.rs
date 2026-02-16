mod ast;
mod codegen;
mod lexer;
mod parser;
mod sema;
mod token;

use std::env;
use std::fs;
use std::path::PathBuf;

use codegen::emit_and_build;
use lexer::Lexer;
use parser::Parser;
use sema::check;

fn main() {
    if let Err(e) = run() {
        eprintln!("error: {e}");
        std::process::exit(1);
    }
}

// Pipeline completo: lexer -> parser -> semántica -> LLVM IR -> objeto/binario.
fn run() -> Result<(), String> {
    let args: Vec<String> = env::args().collect();
    if args.len() < 2 {
        return Err("Uso: neoc <archivo.neoc> [nombre_salida]".to_string());
    }

    let input = &args[1];
    let out_name = args
        .get(2)
        .cloned()
        .unwrap_or_else(|| "programa".to_string());
    let source = fs::read_to_string(input).map_err(|e| format!("No se pudo leer {input}: {e}"))?;

    let mut lexer = Lexer::new(&source);
    let tokens = lexer.tokenize()?;

    let mut parser = Parser::new(tokens);
    let program = parser.parse_program()?;

    check(&program)?;

    let out_dir = PathBuf::from("build");
    let build = emit_and_build(&program, &out_name, &out_dir)?;

    println!("OK: compilación completada");
    println!("- IR: {}", build.ir_path);
    println!("- OBJ: {}", build.obj_path);
    println!("- BIN: {}", build.bin_path);

    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parse_and_check_simple_program() {
        let src = r#"
            extern fn ext_sum(int a, int b) -> int;
            fn main() -> int {
                let int x = ext_sum(2, 3);
                return x + 1;
            }
        "#;

        let mut lex = Lexer::new(src);
        let tokens = lex.tokenize().expect("tokenize");
        let mut parser = Parser::new(tokens);
        let program = parser.parse_program().expect("parse");
        check(&program).expect("semantic check");
    }
}
