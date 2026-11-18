using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Testes.Fakes;

/// <summary>
/// Auditoria do Cofre em memória. Precisa dos socorristas para montar a auditoria com nome e organização.
/// </summary>
public sealed class FakeAcessoCofreRepositorio : IAcessoCofreRepositorio
{
    private readonly List<AcessoCofre> _acessos = [];
    private readonly FakeSocorristaRepositorio _socorristas;

    /// <summary>
    /// Cria o fake.
    /// </summary>
    /// <param name="socorristas">Socorristas, para o nome e a organização na auditoria.</param>
    public FakeAcessoCofreRepositorio(FakeSocorristaRepositorio socorristas)
    {
        _socorristas = socorristas;
    }

    /// <summary>
    /// Acessos gravados, na ordem de gravação.
    /// </summary>
    public IReadOnlyList<AcessoCofre> Acessos => _acessos;

    /// <inheritdoc/>
    public Task<AcessoCofre?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(_acessos.FirstOrDefault(a => a.Id == id));
    }

    /// <inheritdoc/>
    public Task AdicionarAsync(AcessoCofre entidade, CancellationToken cancellationToken)
    {
        _acessos.Add(entidade);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<AcessoCofre?> ObterUltimaConcessaoAsync(Guid socorristaId, Guid entregadorId, CancellationToken cancellationToken)
    {
        return Task.FromResult(_acessos
            .Where(a => a.SocorristaId == socorristaId && a.EntregadorId == entregadorId && a.Concedido)
            .OrderByDescending(a => a.AcessadoEm)
            .FirstOrDefault());
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ItemAuditoriaCofre>> ListarAuditoriaAsync(Guid entregadorId, CancellationToken cancellationToken)
    {
        IReadOnlyList<ItemAuditoriaCofre> itens = _acessos
            .Where(a => a.EntregadorId == entregadorId)
            .Join(
                _socorristas.Socorristas,
                a => a.SocorristaId,
                s => s.Id,
                (a, s) => new ItemAuditoriaCofre(a.AcessadoEm, a.ExpiraEm, a.Origem, a.Concedido, a.Motivo, s.Nome, s.Organizacao))
            .OrderByDescending(i => i.AcessadoEm)
            .ToList();
        return Task.FromResult(itens);
    }
}
