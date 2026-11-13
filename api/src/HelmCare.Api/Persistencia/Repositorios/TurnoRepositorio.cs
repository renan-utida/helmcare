using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;

namespace HelmCare.Api.Persistencia.Repositorios;

/// <summary>
/// Implementação do <see cref="ITurnoRepositorio"/> com o EF Core.
/// </summary>
public sealed class TurnoRepositorio : RepositorioBase<Turno>, ITurnoRepositorio
{
    /// <summary>
    /// Cria o repositório.
    /// </summary>
    /// <param name="contexto">Contexto do EF Core da requisição.</param>
    public TurnoRepositorio(AppDbContext contexto)
        : base(contexto)
    {
    }

    /// <inheritdoc/>
    public Task<Turno?> ObterAsync(Guid id, CancellationToken cancellationToken)
    {
        return Conjunto
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> ExisteAtivoDoEntregadorAsync(Guid entregadorId, CancellationToken cancellationToken)
    {
        // "Ativo" é Fim nulo (Turno.EstaAtivo), o mesmo filtro do índice IX_Turnos_EntregadorId_Ativo.
        return Conjunto.AnyAsync(t => t.EntregadorId == entregadorId && t.Fim == null, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<DadosAlertaTurno?> ObterDadosAlertaAsync(Guid entregadorId, CancellationToken cancellationToken)
    {
        // Um SELECT com duas subconsultas: só as quatro colunas, sem carregar nenhuma entidade.
        return Contexto.Dispositivos
            .AsNoTracking()
            .Where(d => d.EntregadorId == entregadorId)
            .Select(d => new DadosAlertaTurno(
                d.EstadoAtual,
                d.UltimoIndiceCalorC,
                Contexto.EventosImpacto
                    .Where(e => e.DispositivoId == d.Id && e.Resultado == null)
                    .OrderByDescending(e => e.OcorridoEm)
                    .Select(e => e.ContagemTerminaEm)
                    .FirstOrDefault(),
                Contexto.Ocorrencias
                    .Where(o => o.EntregadorId == entregadorId)
                    .Where(OcorrenciaRepositorio.Ativa)
                    .OrderByDescending(o => o.CriadaEm)
                    .Select(o => (Guid?)o.Id)
                    .FirstOrDefault()))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
