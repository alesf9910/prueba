# NeoC — compilador de nivel ingeniería (C#14 + LLVM)

NeoC es un lenguaje estilo C, compilado a nativo con LLVM (`.ll -> .o -> binario`).
Esta implementación está hecha en **C#14 preview** y el núcleo del compilador se modela con **structs** y diseño estático/funcional.

## Objetivos de diseño

- Sintaxis tipo C para funciones y expresiones.
- `struct` estilo C# (campos sin visibilidad: `Tipo nombre;`).
- `extern` para ABI pública entre archivos/módulos.
- `import` y `alias` para módulos.
- Pipeline profesional: Lexer -> Parser -> Semántica -> IR LLVM -> Native.

## Arquitectura (sin clases de dominio)

- `csharp/NeoC.Core/Tokens.cs`: tokens.
- `csharp/NeoC.Core/Lexer.cs`: lexer (`ref struct`).
- `csharp/NeoC.Core/Ast.cs`: AST por `record struct` + enums discriminantes.
- `csharp/NeoC.Core/Parser.cs`: parser (`ref struct`).
- `csharp/NeoC.Core/SemanticAnalyzer.cs`: chequeo semántico estático.
- `csharp/NeoC.Core/LlvmBackend.cs`: generación de IR + ejecución de `clang`.
- `csharp/NeoC.Cli/Program.cs`: CLI del compilador.

## Librería estándar de NeoC

Se incluye una **stdlib modular** bajo `stdlib/` con APIs de sistemas reales:

- `stdlib/core.neoc`: memoria/base (`memcpy`, `assert`, `panic`).
- `stdlib/fs.neoc`: archivos y directorios.
- `stdlib/os.neoc`: procesos, tiempo, entorno, CPU.
- `stdlib/net.neoc`: sockets TCP/UDP y endpoints.
- `stdlib/json.neoc`: parseo/escritura JSON.
- `stdlib/xml.neoc`: parseo y navegación XML.
- `stdlib/hardware.neoc`: CPU/memoria/MMIO/puertos.

> Nota técnica: el frontend ya entiende tipos nombrados (`struct` y firmas), y el backend LLVM actual está cerrado para `int/void` en codegen directo. Los módulos de stdlib quedan con ABI `extern` listos para enlazado runtime.

## Uso

```bash
dotnet build csharp/NeoC.sln -c Release
dotnet run --project csharp/NeoC.Cli -- examples/basic.neoc basic
./build/basic
```

## Estado de ingeniería

- Frontend sólido y extensible.
- Backend nativo funcional para núcleo `int/void`.
- Contratos de stdlib completos por dominio (JSON/XML/Redes/Hardware/FS/OS/Core).
