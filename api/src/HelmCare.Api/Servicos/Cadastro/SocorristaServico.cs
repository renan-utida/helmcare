using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Contratos.Socorristas;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Api.Seguranca;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Api.Servicos.Cadastro;

/// <summary>
/// Cadastro dos socorristas da demonstração (rota de desenvolvimento <c>POST /api/v1/dev/socorristas</c>):
/// o socorrista e a conta dele (papel <c>SOCORRISTA</c>) numa transação só.
/// </summary>
public sealed class SocorristaServico
{
    private readonly ISocorristaRepositorio _socorristas;
    private readonly IUnidadeDeTrabalho _unidadeDeTrabalho;
    private readonly IContasIdentity _contas;

    /// <summary>
    /// Cria o serviço.
    /// </summary>
    /// <param name="socorristas">Repositório de socorristas.</param>
    /// <param name="unidadeDeTrabalho">Grava e abre a transação.</param>
    /// <param name="contas">Contas do Identity.</param>
    public SocorristaServico(ISocorristaRepositorio socorristas, IUnidadeDeTrabalho unidadeDeTrabalho, IContasIdentity contas)
    {
        _socorristas = socorristas;
        _unidadeDeTrabalho = unidadeDeTrabalho;
        _contas = contas;
    }

    /// <summary>
    /// Cria o socorrista e a conta. Se uma das gravações falhar, nada fica gravado.
    /// </summary>
    /// <param name="requisicao">Dados do socorrista, e-mail e senha.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O socorrista criado, com o e-mail mascarado.</returns>
    /// <exception cref="ValidacaoDominioException">Dados inválidos (400).</exception>
    /// <exception cref="RegraNegocioException">E-mail repetido (409).</exception>
    public async Task<SocorristaResposta> CriarAsync(CriarSocorristaRequisicao requisicao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        var socorrista = new Socorrista(requisicao.Nome, requisicao.RegistroProfissional, requisicao.Organizacao);

        if (requisicao.Verificado)
        {
            socorrista.Verificar();
        }

        if (await _contas.EmailEmUsoAsync(requisicao.Email, cancellationToken))
        {
            throw new RegraNegocioException(IContasIdentity.MensagemEmailRepetido);
        }

        // Mesmo padrão do cadastro do entregador: a conta primeiro, depois o perfil, e o commit no fim.
        await using ITransacao transacao = await _unidadeDeTrabalho.IniciarTransacaoAsync(cancellationToken);

        string usuarioId = await _contas.CriarAsync(requisicao.Email, requisicao.Senha, Papeis.Socorrista, cancellationToken);
        socorrista.VincularUsuario(usuarioId);
        await _socorristas.AdicionarAsync(socorrista, cancellationToken);

        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
        await transacao.ConfirmarAsync(cancellationToken);

        return SocorristaResposta.De(socorrista, requisicao.Email);
    }
}
