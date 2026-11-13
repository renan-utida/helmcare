using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using Microsoft.EntityFrameworkCore;

namespace HelmCare.Api.Persistencia.Repositorios;

/// <summary>
/// Implementação do <see cref="IOcorrenciaRepositorio"/> com o EF Core.
/// </summary>
public sealed class OcorrenciaRepositorio : RepositorioBase<Ocorrencia>, IOcorrenciaRepositorio
{
    /// <summary>
    /// Filtro de ocorrência ativa que o banco consegue executar. A propriedade
    /// <see cref="Ocorrencia.EstaAtiva"/> é calculada em C# e não vira SQL, então os mesmos
    /// estados são repetidos aqui; um teste confere que os dois concordam em todo estado.
    /// </summary>
    public static readonly Expression<Func<Ocorrencia, bool>> Ativa = o =>
        o.Estado == EstadoOcorrencia.SosCamada1
        || o.Estado == EstadoOcorrencia.SosCamada2
        || o.Estado == EstadoOcorrencia.SocorroACaminho;

    /// <summary>
    /// Cria o repositório.
    /// </summary>
    /// <param name="contexto">Contexto do EF Core da operação.</param>
    public OcorrenciaRepositorio(AppDbContext contexto)
        : base(contexto)
    {
    }

    /// <inheritdoc/>
    public Task<bool> ExisteAtivaDoEntregadorAsync(Guid entregadorId, CancellationToken cancellationToken)
    {
        return Conjunto
            .Where(o => o.EntregadorId == entregadorId)
            .AnyAsync(Ativa, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<Ocorrencia?> ObterAsync(Guid id, CancellationToken cancellationToken)
    {
        return Conjunto
            .Include(o => o.EventoImpacto)
            .Include(o => o.Aceites)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<Ocorrencia?> ObterParaConsultaAsync(Guid id, CancellationToken cancellationToken)
    {
        return Conjunto
            .AsNoTracking()
            .Include(o => o.EventoImpacto)
            .Include(o => o.Aceites)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }
}
