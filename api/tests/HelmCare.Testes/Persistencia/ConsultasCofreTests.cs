using System;
using HelmCare.Api.Persistencia;
using HelmCare.Api.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Persistencia;

/// <summary>
/// Confere, sem banco, que o EF Core traduz para SQL as consultas do Cofre: o contexto usa uma
/// connection string fictícia, e o <c>ToQueryString</c> só monta o SQL, sem abrir conexão. Os fakes em
/// memória não pegam uma consulta que o EF não traduz (ela roda direto no LINQ).
/// </summary>
[TestClass]
public sealed class ConsultasCofreTests
{
    /// <summary>
    /// Garante que a auditoria vira um SQL com o JOIN dos socorristas e o ORDER BY decrescente, sem o ip.
    /// </summary>
    [TestMethod]
    public void ConsultaAuditoria_Traduz_ComJoinEOrdemDecrescente()
    {
        // Arrange
        using AppDbContext contexto = Contexto();

        // Act
        string sql = AcessoCofreRepositorio.ConsultaAuditoria(contexto, Guid.NewGuid()).ToQueryString();

        // Assert
        Assert.Contains("JOIN \"Socorristas\"", sql);
        Assert.Contains("ORDER BY a.\"AcessadoEm\" DESC", sql);
        Assert.DoesNotContain("\"Ip\"", sql);
    }

    /// <summary>
    /// Garante que a última concessão do par vira um SQL só com as concedidas, da mais recente à mais antiga.
    /// </summary>
    [TestMethod]
    public void ConsultaConcessoesDoPar_Traduz_SoConcedidas()
    {
        // Arrange
        using AppDbContext contexto = Contexto();

        // Act
        string sql = AcessoCofreRepositorio.ConsultaConcessoesDoPar(contexto, Guid.NewGuid(), Guid.NewGuid()).ToQueryString();

        // Assert
        Assert.Contains("\"Concedido\"", sql);
        Assert.Contains("ORDER BY a.\"AcessadoEm\" DESC", sql);
    }

    private static AppDbContext Contexto()
    {
        DbContextOptions<AppDbContext> opcoes = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=consulta_ficticia;Username=ficticio")
            .Options;
        return new AppDbContext(opcoes);
    }
}
