using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Mensageria;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Dominio.Enums;

namespace HelmCare.Testes.Fakes;

/// <summary>
/// Publicador de comandos em memória: guarda os comandos na ordem e pode simular o broker fora do ar.
/// </summary>
public sealed class FakePublicadorComandos : IPublicadorComandos
{
    private readonly List<ComandoPublicadoFake> _comandos = [];

    /// <summary>
    /// Comandos publicados com sucesso, na ordem.
    /// </summary>
    public IReadOnlyList<ComandoPublicadoFake> Comandos => _comandos;

    /// <summary>
    /// Quando verdadeiro, toda publicação lança <see cref="MensageriaIndisponivelException"/> e nada é guardado.
    /// </summary>
    public bool Indisponivel { get; set; }

    /// <inheritdoc/>
    public Task PublicarAsync(string codigoDispositivo, TipoComandoMqtt tipo, CancellationToken cancellationToken)
    {
        if (Indisponivel)
        {
            throw new MensageriaIndisponivelException("Broker simulado fora do ar.");
        }

        _comandos.Add(new ComandoPublicadoFake(codigoDispositivo, tipo));
        return Task.CompletedTask;
    }
}

/// <summary>
/// Um comando guardado pelo <see cref="FakePublicadorComandos"/>.
/// </summary>
/// <param name="Codigo">Código do capacete.</param>
/// <param name="Tipo">Comando.</param>
public sealed record ComandoPublicadoFake(string Codigo, TipoComandoMqtt Tipo);
