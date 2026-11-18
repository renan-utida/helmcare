using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;

namespace HelmCare.Api.Persistencia.Repositorios;

/// <summary>
/// Implementação do <see cref="IAcessoCofreRepositorio"/> com o EF Core.
/// </summary>
public sealed class AcessoCofreRepositorio : RepositorioBase<AcessoCofre>, IAcessoCofreRepositorio
{
    /// <summary>
    /// Cria o repositório.
    /// </summary>
    /// <param name="contexto">Contexto do EF Core da requisição.</param>
    public AcessoCofreRepositorio(AppDbContext contexto)
        : base(contexto)
    {
    }

    /// <inheritdoc/>
    public Task<AcessoCofre?> ObterUltimaConcessaoAsync(Guid socorristaId, Guid entregadorId, CancellationToken cancellationToken)
    {
        return ConsultaConcessoesDoPar(Contexto, socorristaId, entregadorId).FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ItemAuditoriaCofre>> ListarAuditoriaAsync(Guid entregadorId, CancellationToken cancellationToken)
    {
        return await ConsultaAuditoria(Contexto, entregadorId).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Consulta da auditoria do entregador, pública para o teste conferir que o EF Core a traduz para SQL
    /// sem banco (<c>ToQueryString</c>). A ordenação vem antes da projeção: o EF não traduz um
    /// <c>OrderBy</c> sobre um membro de um record montado pelo construtor.
    /// </summary>
    /// <param name="contexto">Contexto do EF Core.</param>
    /// <param name="entregadorId">Entregador dono da ficha.</param>
    /// <returns>A consulta, do acesso mais recente ao mais antigo.</returns>
    public static IQueryable<ItemAuditoriaCofre> ConsultaAuditoria(AppDbContext contexto, Guid entregadorId)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        return contexto.AcessosCofre
            .AsNoTracking()
            .Where(a => a.EntregadorId == entregadorId)
            .Join(contexto.Socorristas, a => a.SocorristaId, s => s.Id, (a, s) => new { Acesso = a, Socorrista = s })
            .OrderByDescending(x => x.Acesso.AcessadoEm)
            .Select(x => new ItemAuditoriaCofre(
                x.Acesso.AcessadoEm,
                x.Acesso.ExpiraEm,
                x.Acesso.Origem,
                x.Acesso.Concedido,
                x.Acesso.Motivo,
                x.Socorrista.Nome,
                x.Socorrista.Organizacao));
    }

    /// <summary>
    /// Consulta da última concessão do par, pública para o mesmo teste de tradução.
    /// </summary>
    /// <param name="contexto">Contexto do EF Core.</param>
    /// <param name="socorristaId">Socorrista.</param>
    /// <param name="entregadorId">Entregador dono da ficha.</param>
    /// <returns>A consulta das concessões do par, da mais recente à mais antiga.</returns>
    public static IQueryable<AcessoCofre> ConsultaConcessoesDoPar(AppDbContext contexto, Guid socorristaId, Guid entregadorId)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        return contexto.AcessosCofre
            .AsNoTracking()
            .Where(a => a.SocorristaId == socorristaId && a.EntregadorId == entregadorId && a.Concedido)
            .OrderByDescending(a => a.AcessadoEm);
    }
}
