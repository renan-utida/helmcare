using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Seguranca;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Testes.Fakes;

/// <summary>
/// Autorização que deixa tudo passar, para os testes anteriores à 1.15 continuarem provando só a regra
/// de negócio deles. As regras de quem pode o quê são testadas com o <see cref="AutorizacaoRecursos"/>
/// real, nos testes de autorização.
/// </summary>
public sealed class FakeAutorizacaoLivre : IAutorizacaoRecursos
{
    /// <inheritdoc/>
    public Guid? EntregadorDoToken { get; set; }

    /// <inheritdoc/>
    public Guid ExigirEntregador()
    {
        return EntregadorDoToken ?? Guid.Empty;
    }

    /// <inheritdoc/>
    public void ExigirDono(Guid entregadorId)
    {
    }

    /// <inheritdoc/>
    public Task ExigirLeituraOcorrenciaAsync(Ocorrencia ocorrencia, Voluntario? voluntarioInformado, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ExigirEncerramentoAsync(Ocorrencia ocorrencia, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
