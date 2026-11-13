using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Testes.Fakes;

/// <summary>
/// Repositório de turnos em memória. Os dados crus do alerta são definidos pelo teste em
/// <see cref="DefinirDadosAlerta"/>, para conferir as regras que o serviço aplica sobre eles.
/// </summary>
public sealed class FakeTurnoRepositorio : ITurnoRepositorio
{
    private readonly List<Turno> _turnos = [];
    private readonly Dictionary<Guid, DadosAlertaTurno> _dadosAlerta = [];

    /// <summary>
    /// Turnos adicionados ou semeados.
    /// </summary>
    public IReadOnlyList<Turno> Turnos => _turnos;

    /// <summary>
    /// Coloca um turno na lista, como se já estivesse no banco.
    /// </summary>
    /// <param name="turno">Turno existente.</param>
    public void Semear(Turno turno)
    {
        _turnos.Add(turno);
    }

    /// <summary>
    /// Define o que a consulta do alerta devolve para o entregador (sem chamar, devolve nulo: sem capacete).
    /// </summary>
    /// <param name="entregadorId">Entregador do turno.</param>
    /// <param name="dados">Dados crus da consulta.</param>
    public void DefinirDadosAlerta(Guid entregadorId, DadosAlertaTurno dados)
    {
        _dadosAlerta[entregadorId] = dados;
    }

    /// <inheritdoc/>
    public Task<Turno?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(_turnos.FirstOrDefault(t => t.Id == id));
    }

    /// <inheritdoc/>
    public Task<Turno?> ObterAsync(Guid id, CancellationToken cancellationToken)
    {
        return ObterPorIdAsync(id, cancellationToken);
    }

    /// <inheritdoc/>
    public Task AdicionarAsync(Turno entidade, CancellationToken cancellationToken)
    {
        _turnos.Add(entidade);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> ExisteAtivoDoEntregadorAsync(Guid entregadorId, CancellationToken cancellationToken)
    {
        return Task.FromResult(_turnos.Exists(t => t.EntregadorId == entregadorId && t.EstaAtivo));
    }

    /// <inheritdoc/>
    public Task<DadosAlertaTurno?> ObterDadosAlertaAsync(Guid entregadorId, CancellationToken cancellationToken)
    {
        return Task.FromResult(_dadosAlerta.GetValueOrDefault(entregadorId));
    }
}
