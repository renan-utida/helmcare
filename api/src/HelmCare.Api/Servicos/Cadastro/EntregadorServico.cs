using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Contratos.Entregadores;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Api.Servicos.Cadastro;

/// <summary>
/// Casos de uso do cadastro do entregador: criar, consultar, definir a ficha médica e adicionar
/// contatos de emergência. As regras de formato ficam no domínio; aqui ficam as que dependem do
/// banco (CPF repetido, entregador inexistente). Recebe DTOs e devolve DTOs, nunca entidades.
/// </summary>
public sealed class EntregadorServico
{
    private readonly IEntregadorRepositorio _entregadores;
    private readonly IUnidadeDeTrabalho _unidadeDeTrabalho;
    private readonly TimeProvider _relogio;

    /// <summary>
    /// Cria o serviço.
    /// </summary>
    /// <param name="entregadores">Repositório de entregadores.</param>
    /// <param name="unidadeDeTrabalho">Confirma a gravação no fim de cada operação.</param>
    /// <param name="relogio">Fonte do instante atual (o <c>criadoEm</c>), trocada nos testes.</param>
    public EntregadorServico(
        IEntregadorRepositorio entregadores,
        IUnidadeDeTrabalho unidadeDeTrabalho,
        TimeProvider relogio)
    {
        _entregadores = entregadores;
        _unidadeDeTrabalho = unidadeDeTrabalho;
        _relogio = relogio;
    }

    /// <summary>
    /// Cadastra um entregador.
    /// </summary>
    /// <param name="requisicao">Dados do cadastro.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O entregador criado, com a PII mascarada.</returns>
    /// <exception cref="ValidacaoDominioException">Quando algum dado é inválido (400).</exception>
    /// <exception cref="RegraNegocioException">Quando o CPF já está cadastrado (409).</exception>
    public async Task<EntregadorResposta> CriarAsync(
        CriarEntregadorRequisicao requisicao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        // A entidade é criada antes da consulta porque é ela que normaliza o CPF (só dígitos):
        // "111.444.777-35" e "11144477735" precisam bater como o mesmo CPF.
        var entregador = new Entregador(
            requisicao.Nome,
            requisicao.Cpf,
            requisicao.Cnh,
            requisicao.Telefone,
            _relogio.GetUtcNow().UtcDateTime);

        if (await _entregadores.CpfJaCadastradoAsync(entregador.Cpf, cancellationToken))
        {
            // A mensagem não repete o CPF (PII).
            throw new RegraNegocioException("Já existe um entregador cadastrado com este CPF.");
        }

        await _entregadores.AdicionarAsync(entregador, cancellationToken);
        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);

        return EntregadorResposta.De(entregador);
    }

    /// <summary>
    /// Consulta o entregador com os contatos, sem o conteúdo da ficha médica.
    /// </summary>
    /// <param name="id">Id do entregador.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O entregador, com a PII mascarada.</returns>
    /// <exception cref="RecursoNaoEncontradoException">Quando o entregador não existe (404).</exception>
    public async Task<EntregadorResposta> ObterAsync(Guid id, CancellationToken cancellationToken)
    {
        Entregador entregador = await _entregadores.ObterComCadastroAsync(id, cancellationToken)
            ?? throw EntregadorNaoEncontrado();

        return EntregadorResposta.De(entregador);
    }

    /// <summary>
    /// Cria a ficha médica na primeira chamada e a substitui nas seguintes (uma ficha por entregador).
    /// </summary>
    /// <param name="id">Id do entregador.</param>
    /// <param name="requisicao">Dados da ficha.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>A ficha gravada, para quem acabou de enviá-la.</returns>
    /// <exception cref="RecursoNaoEncontradoException">Quando o entregador não existe (404).</exception>
    /// <exception cref="ValidacaoDominioException">Quando algum dado da ficha é inválido (400).</exception>
    public async Task<FichaMedicaResposta> DefinirFichaMedicaAsync(
        Guid id,
        FichaMedicaRequisicao requisicao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        // Com a ficha carregada, o domínio sabe se atualiza a existente ou cria uma nova.
        Entregador entregador = await _entregadores.ObterComCadastroAsync(id, cancellationToken)
            ?? throw EntregadorNaoEncontrado();

        FichaMedica ficha = entregador.DefinirFichaMedica(
            requisicao.TipoSanguineo,
            requisicao.Alergias,
            requisicao.CondicoesCriticas,
            requisicao.Medicamentos);

        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);

        return FichaMedicaResposta.De(ficha);
    }

    /// <summary>
    /// Adiciona um contato de emergência ao entregador.
    /// </summary>
    /// <param name="id">Id do entregador.</param>
    /// <param name="requisicao">Dados do contato.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O contato criado, com o telefone mascarado.</returns>
    /// <exception cref="RecursoNaoEncontradoException">Quando o entregador não existe (404).</exception>
    /// <exception cref="ValidacaoDominioException">Quando algum dado do contato é inválido (400).</exception>
    public async Task<ContatoResposta> AdicionarContatoAsync(
        Guid id,
        ContatoRequisicao requisicao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        // Não precisa carregar os contatos existentes: o novo só é acrescentado.
        Entregador entregador = await _entregadores.ObterPorIdAsync(id, cancellationToken)
            ?? throw EntregadorNaoEncontrado();

        ContatoEmergencia contato = entregador.AdicionarContato(
            requisicao.Nome,
            requisicao.Telefone,
            requisicao.Parentesco);

        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);

        return ContatoResposta.De(contato);
    }

    private static RecursoNaoEncontradoException EntregadorNaoEncontrado()
    {
        return new RecursoNaoEncontradoException("Entregador não encontrado.");
    }
}
