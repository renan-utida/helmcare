using Microsoft.VisualStudio.TestTools.UnitTesting;

// Roda os métodos de teste em paralelo: os testes não compartilham estado.
[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]
