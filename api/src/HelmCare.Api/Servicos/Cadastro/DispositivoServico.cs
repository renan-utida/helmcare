using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Contratos.Dispositivos;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Api.Servicos.Cadastro;

/// <summary>
/// Vínculo do capacete com o entregador. Cada código existe uma vez só, e cada entregador tem
/// no máximo um capacete. O banco também garante as duas regras com índices únicos; a checagem
/// aqui existe para responder 409 com uma mensagem clara, antes de tentar gravar.
/// </summary>
public sealed class DispositivoServico
{
    private readonly IDispositivoRepositorio _dispositivos;
    private readonly IEntregadorRepositorio _entregadores;
    private readonly IUnidadeDeTrabalho _unidadeDeTrabalho;

    /// <summary>
    /// Cria o serviço.
    /// </summary>
    /// <param name="dispositivos">Repositório de capacetes.</param>
    /// <param name="entregadores">Repositório de entregadores (para conferir se o dono existe).</param>
    /// <param name="unidadeDeTrabalho">Confirma a gravação.</param>
    public DispositivoServico(
        IDispositivoRepositorio dispositivos,
        IEntregadorRepositorio entregadores,
        IUnidadeDeTrabalho unidadeDeTrabalho)
    {
        _dispositivos = dispositivos;
        _entregadores = entregadores;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    /// <summary>
    /// Vincula um capacete novo ao entregador e gera o QR token.
    /// </summary>
    /// <param name="requisicao">Código do capacete e id do entregador.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O capacete vinculado, com o QR token e a URL do Cofre.</returns>
    /// <exception cref="ValidacaoDominioException">Quando o código ou o id são inválidos (400).</exception>
    /// <exception cref="RecursoNaoEncontradoException">Quando o entregador não existe (404).</exception>
    /// <exception cref="RegraNegocioException">Quando o código já existe ou o entregador já tem capacete (409).</exception>
    public async Task<DispositivoResposta> VincularAsync(
        VincularDispositivoRequisicao requisicao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        // O domínio valida primeiro (formato do código, id informado): dado inválido é 400 antes
        // de qualquer consulta. Guid.Empty no lugar do nulo cai na mesma validação.
        var dispositivo = new Dispositivo(requisicao.Codigo, requisicao.EntregadorId ?? Guid.Empty);

        if (await _entregadores.ObterPorIdAsync(dispositivo.EntregadorId, cancellationToken) is null)
        {
            throw new RecursoNaoEncontradoException("Entregador não encontrado.");
        }

        if (await _dispositivos.CodigoJaCadastradoAsync(dispositivo.Codigo, cancellationToken))
        {
            throw new RegraNegocioException($"O capacete {dispositivo.Codigo} já está vinculado a um entregador.");
        }

        if (await _dispositivos.EntregadorJaTemDispositivoAsync(dispositivo.EntregadorId, cancellationToken))
        {
            throw new RegraNegocioException("O entregador já tem um capacete vinculado.");
        }

        await _dispositivos.AdicionarAsync(dispositivo, cancellationToken);
        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);

        return DispositivoResposta.De(dispositivo);
    }
}
