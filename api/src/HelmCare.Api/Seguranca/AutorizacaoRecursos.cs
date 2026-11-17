using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Api.Seguranca;

/// <summary>
/// <see cref="IAutorizacaoRecursos"/> a partir do <see cref="IUsuarioAtual"/>. Os textos do 403 não
/// dizem de quem é o recurso, só que não é de quem pediu.
/// </summary>
public sealed class AutorizacaoRecursos : IAutorizacaoRecursos
{
    /// <summary>
    /// Mensagem do <c>401</c> de uma rota protegida sem token válido.
    /// </summary>
    public const string MensagemSemToken = "Faça o login: o token de acesso está ausente, é inválido ou venceu.";

    /// <summary>
    /// Mensagem do <c>403</c> de um token que não é de entregador (por exemplo, de socorrista).
    /// </summary>
    public const string MensagemPapelErrado = "Esta rota é só para entregadores.";

    /// <summary>
    /// Mensagem do <c>403</c> de um recurso de outro entregador.
    /// </summary>
    public const string MensagemOutroDono = "O recurso pertence a outro usuário.";

    private readonly IUsuarioAtual _usuario;
    private readonly IVoluntarioRepositorio _voluntarios;

    /// <summary>
    /// Cria o componente.
    /// </summary>
    /// <param name="usuario">Quem fez a requisição.</param>
    /// <param name="voluntarios">Voluntários, para as regras da ocorrência.</param>
    public AutorizacaoRecursos(IUsuarioAtual usuario, IVoluntarioRepositorio voluntarios)
    {
        _usuario = usuario;
        _voluntarios = voluntarios;
    }

    /// <inheritdoc/>
    public Guid? EntregadorDoToken => _usuario.EhEntregador ? _usuario.EntregadorId : null;

    /// <inheritdoc/>
    public Guid ExigirEntregador()
    {
        if (!_usuario.Autenticado)
        {
            throw new NaoAutenticadoException(MensagemSemToken);
        }

        if (!_usuario.EhEntregador || _usuario.EntregadorId is not { } entregadorId)
        {
            throw new AcessoNegadoException(MensagemPapelErrado);
        }

        return entregadorId;
    }

    /// <inheritdoc/>
    public void ExigirDono(Guid entregadorId)
    {
        if (ExigirEntregador() != entregadorId)
        {
            throw new AcessoNegadoException(MensagemOutroDono);
        }
    }

    /// <inheritdoc/>
    public async Task ExigirLeituraOcorrenciaAsync(
        Ocorrencia ocorrencia,
        Voluntario? voluntarioInformado,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ocorrencia);

        Guid eu = ExigirEntregador();

        // Com o voluntarioId na consulta, ele precisa ser o do token; e quem é voluntário pode ver.
        if (voluntarioInformado is not null)
        {
            ExigirDono(voluntarioInformado.EntregadorId);
            return;
        }

        if (ocorrencia.EntregadorId == eu || await _voluntarios.ExisteDoEntregadorAsync(eu, cancellationToken))
        {
            return;
        }

        throw new AcessoNegadoException(MensagemOutroDono);
    }

    /// <inheritdoc/>
    public async Task ExigirEncerramentoAsync(Ocorrencia ocorrencia, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ocorrencia);

        Guid eu = ExigirEntregador();

        if (ocorrencia.EntregadorId == eu)
        {
            return;
        }

        if (ocorrencia.Aceites.FirstOrDefault() is { } aceite
            && await _voluntarios.ObterParaConsultaAsync(aceite.VoluntarioId, cancellationToken) is { } quemAceitou
            && quemAceitou.EntregadorId == eu)
        {
            return;
        }

        throw new AcessoNegadoException(MensagemOutroDono);
    }
}
