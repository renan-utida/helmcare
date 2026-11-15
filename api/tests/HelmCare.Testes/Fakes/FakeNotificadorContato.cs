using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Servicos.Ocorrencias;

namespace HelmCare.Testes.Fakes;

/// <summary>
/// Notificador em memória: guarda cada aviso com quantas gravações já tinham acontecido naquele
/// momento, para o teste provar que o aviso veio depois do <c>SalvarAsync</c>. Pode simular uma falha.
/// </summary>
public sealed class FakeNotificadorContato : INotificadorContato
{
    private readonly FakeUnidadeDeTrabalho _unidade;
    private readonly List<AvisoContatoFake> _avisos = [];

    /// <summary>
    /// Cria o fake ligado à unidade de trabalho do cenário.
    /// </summary>
    /// <param name="unidade">Unidade de trabalho cujas gravações são contadas.</param>
    public FakeNotificadorContato(FakeUnidadeDeTrabalho unidade)
    {
        _unidade = unidade;
    }

    /// <summary>
    /// Avisos recebidos, na ordem.
    /// </summary>
    public IReadOnlyList<AvisoContatoFake> Avisos => _avisos;

    /// <summary>
    /// Quando verdadeiro, o aviso lança <see cref="InvalidOperationException"/> depois de registrar a chamada.
    /// </summary>
    public bool Falhar { get; set; }

    /// <inheritdoc/>
    public Task NotificarAberturaAsync(Guid ocorrenciaId, int quantidadeContatos, CancellationToken cancellationToken)
    {
        _avisos.Add(new AvisoContatoFake(ocorrenciaId, quantidadeContatos, _unidade.VezesSalvo));

        if (Falhar)
        {
            throw new InvalidOperationException("Falha simulada no aviso.");
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Um aviso guardado pelo <see cref="FakeNotificadorContato"/>.
/// </summary>
/// <param name="OcorrenciaId">Ocorrência avisada.</param>
/// <param name="QuantidadeContatos">Quantidade de contatos informada.</param>
/// <param name="GravacoesAntes">Quantas gravações já tinham terminado quando o aviso chegou.</param>
public sealed record AvisoContatoFake(Guid OcorrenciaId, int QuantidadeContatos, int GravacoesAntes);
