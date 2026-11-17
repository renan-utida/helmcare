using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Contratos.Entregadores;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Api.Seguranca;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Api.Servicos.Cadastro;

/// <summary>
/// Casos de uso do cadastro do entregador: criar (com a conta de e-mail e senha), consultar, ficha
/// médica, contatos de emergência e telefone. As regras de formato ficam no domínio; aqui ficam as
/// que dependem do banco (CPF, CNH, telefone e e-mail repetidos, recurso inexistente) e a conferência
/// do dono, sempre depois de achar o recurso (ordem 401, 404, 403). Recebe e devolve DTOs.
/// </summary>
public sealed class EntregadorServico
{
    /// <summary>
    /// Mensagem do <c>409</c> de CPF repetido (também usada na corrida pelo índice).
    /// </summary>
    public const string MensagemCpfRepetido = "Já existe um entregador cadastrado com este CPF.";

    /// <summary>
    /// Mensagem do <c>409</c> de CNH repetida.
    /// </summary>
    public const string MensagemCnhRepetida = "Já existe um entregador cadastrado com esta CNH.";

    /// <summary>
    /// Mensagem do <c>409</c> de telefone já usado por outro entregador.
    /// </summary>
    public const string MensagemTelefoneRepetido = "Já existe um entregador cadastrado com este telefone.";

    private readonly IEntregadorRepositorio _entregadores;
    private readonly IUnidadeDeTrabalho _unidadeDeTrabalho;
    private readonly IContasIdentity _contas;
    private readonly IAutorizacaoRecursos _autorizacao;
    private readonly TimeProvider _relogio;

    /// <summary>
    /// Cria o serviço.
    /// </summary>
    /// <param name="entregadores">Repositório de entregadores.</param>
    /// <param name="unidadeDeTrabalho">Confirma a gravação e abre a transação do cadastro.</param>
    /// <param name="contas">Contas do Identity (e-mail e senha).</param>
    /// <param name="autorizacao">Conferência do dono do recurso.</param>
    /// <param name="relogio">Fonte do instante atual (o <c>criadoEm</c>), trocada nos testes.</param>
    public EntregadorServico(
        IEntregadorRepositorio entregadores,
        IUnidadeDeTrabalho unidadeDeTrabalho,
        IContasIdentity contas,
        IAutorizacaoRecursos autorizacao,
        TimeProvider relogio)
    {
        _entregadores = entregadores;
        _unidadeDeTrabalho = unidadeDeTrabalho;
        _contas = contas;
        _autorizacao = autorizacao;
        _relogio = relogio;
    }

    /// <summary>
    /// Cadastra um entregador e cria a conta dele (papel <c>ENTREGADOR</c>) na mesma transação: se uma
    /// das gravações falhar, nada fica gravado. Rota pública; não devolve token.
    /// </summary>
    /// <param name="requisicao">Dados do cadastro, com e-mail e senha.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O entregador criado, com a PII mascarada.</returns>
    /// <exception cref="ValidacaoDominioException">Quando algum dado é inválido (400).</exception>
    /// <exception cref="RegraNegocioException">CPF, CNH, telefone ou e-mail repetido (409).</exception>
    public async Task<EntregadorResposta> CriarAsync(
        CriarEntregadorRequisicao requisicao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        // A entidade é criada antes das consultas porque é ela que normaliza os documentos (só
        // dígitos): "111.444.777-35" e "11144477735" precisam bater como o mesmo CPF.
        var entregador = new Entregador(
            requisicao.Nome,
            requisicao.Cpf,
            requisicao.Cnh,
            requisicao.Telefone,
            _relogio.GetUtcNow().UtcDateTime);

        // As mensagens não repetem o valor recebido (PII).
        if (await _entregadores.CpfJaCadastradoAsync(entregador.Cpf, cancellationToken))
        {
            throw new RegraNegocioException(MensagemCpfRepetido);
        }

        if (await _entregadores.CnhJaCadastradaAsync(entregador.Cnh, cancellationToken))
        {
            throw new RegraNegocioException(MensagemCnhRepetida);
        }

        if (await _entregadores.TelefoneEmUsoAsync(entregador.Telefone, null, cancellationToken))
        {
            throw new RegraNegocioException(MensagemTelefoneRepetido);
        }

        await CriarContaVinculadaAsync(entregador, requisicao.Email, requisicao.Senha, novo: true, cancellationToken);

        return EntregadorResposta.De(entregador, requisicao.Email);
    }

    /// <summary>
    /// Cria a conta de um entregador que já existia antes da 1.15 (rota de desenvolvimento), com as
    /// mesmas regras de e-mail e senha do cadastro.
    /// </summary>
    /// <param name="id">Id do entregador.</param>
    /// <param name="requisicao">E-mail e senha.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O entregador, com o e-mail mascarado.</returns>
    /// <exception cref="RecursoNaoEncontradoException">Entregador inexistente (404).</exception>
    /// <exception cref="RegraNegocioException">Entregador que já tem conta ou e-mail repetido (409).</exception>
    public async Task<EntregadorResposta> CriarContaAsync(
        Guid id,
        ContaRequisicao requisicao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        Entregador entregador = await _entregadores.ObterComCadastroAsync(id, cancellationToken)
            ?? throw EntregadorNaoEncontrado();

        if (entregador.UsuarioId is not null)
        {
            throw new RegraNegocioException("O entregador já tem uma conta.");
        }

        await CriarContaVinculadaAsync(entregador, requisicao.Email, requisicao.Senha, novo: false, cancellationToken);

        return EntregadorResposta.De(entregador, requisicao.Email);
    }

    /// <summary>
    /// Consulta o entregador com os contatos, sem o conteúdo da ficha médica. Só o próprio entregador.
    /// </summary>
    /// <param name="id">Id do entregador.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O entregador, com a PII mascarada.</returns>
    /// <exception cref="RecursoNaoEncontradoException">Quando o entregador não existe (404).</exception>
    /// <exception cref="AcessoNegadoException">Outro entregador ou papel errado (403).</exception>
    public async Task<EntregadorResposta> ObterAsync(Guid id, CancellationToken cancellationToken)
    {
        Entregador entregador = await ObterDoDonoAsync(id, cancellationToken);
        return await RespostaAsync(entregador, cancellationToken);
    }

    /// <summary>
    /// Devolve a ficha médica completa. Só o próprio entregador.
    /// </summary>
    /// <param name="id">Id do entregador.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>A ficha.</returns>
    /// <exception cref="RecursoNaoEncontradoException">Entregador inexistente ou sem ficha (404).</exception>
    /// <exception cref="AcessoNegadoException">Outro entregador ou papel errado (403).</exception>
    public async Task<FichaMedicaResposta> ObterFichaMedicaAsync(Guid id, CancellationToken cancellationToken)
    {
        Entregador entregador = await ObterDoDonoAsync(id, cancellationToken);

        FichaMedica ficha = entregador.FichaMedica
            ?? throw new RecursoNaoEncontradoException("O entregador não tem ficha médica cadastrada.");

        return FichaMedicaResposta.De(ficha);
    }

    /// <summary>
    /// Cria a ficha médica na primeira chamada e a substitui nas seguintes (uma ficha por entregador).
    /// </summary>
    /// <param name="id">Id do entregador.</param>
    /// <param name="requisicao">Dados da ficha.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>A ficha gravada, para quem acabou de enviá-la.</returns>
    /// <exception cref="RecursoNaoEncontradoException">Quando o entregador não existe (404).</exception>
    /// <exception cref="AcessoNegadoException">Outro entregador ou papel errado (403).</exception>
    /// <exception cref="ValidacaoDominioException">Quando algum dado da ficha é inválido (400).</exception>
    public async Task<FichaMedicaResposta> DefinirFichaMedicaAsync(
        Guid id,
        FichaMedicaRequisicao requisicao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        // Com a ficha carregada, o domínio sabe se atualiza a existente ou cria uma nova.
        Entregador entregador = await ObterDoDonoAsync(id, cancellationToken);

        FichaMedica ficha = entregador.DefinirFichaMedica(
            requisicao.TipoSanguineo,
            requisicao.Alergias,
            requisicao.CondicoesCriticas,
            requisicao.Medicamentos);

        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);

        return FichaMedicaResposta.De(ficha);
    }

    /// <summary>
    /// Troca o telefone do entregador.
    /// </summary>
    /// <param name="id">Id do entregador.</param>
    /// <param name="requisicao">Telefone novo.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O corpo do <c>GET /entregadores/{id}</c>.</returns>
    /// <exception cref="RecursoNaoEncontradoException">Quando o entregador não existe (404).</exception>
    /// <exception cref="AcessoNegadoException">Outro entregador ou papel errado (403).</exception>
    /// <exception cref="ValidacaoDominioException">Telefone inválido (400).</exception>
    /// <exception cref="RegraNegocioException">Telefone já usado por outro entregador (409).</exception>
    public async Task<EntregadorResposta> TrocarTelefoneAsync(
        Guid id,
        TelefoneRequisicao requisicao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        Entregador entregador = await ObterDoDonoAsync(id, cancellationToken);

        // O domínio normaliza (só dígitos) antes da consulta; se der 409, a troca em memória é
        // descartada com o escopo da requisição, sem chegar ao banco.
        entregador.TrocarTelefone(requisicao.Telefone);

        if (await _entregadores.TelefoneEmUsoAsync(entregador.Telefone, entregador.Id, cancellationToken))
        {
            throw new RegraNegocioException(MensagemTelefoneRepetido);
        }

        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);

        return await RespostaAsync(entregador, cancellationToken);
    }

    /// <summary>
    /// Adiciona um contato de emergência ao entregador.
    /// </summary>
    /// <param name="id">Id do entregador.</param>
    /// <param name="requisicao">Dados do contato.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O contato criado, com o telefone mascarado.</returns>
    /// <exception cref="RecursoNaoEncontradoException">Quando o entregador não existe (404).</exception>
    /// <exception cref="AcessoNegadoException">Outro entregador ou papel errado (403).</exception>
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
        _autorizacao.ExigirDono(entregador.Id);

        ContatoEmergencia contato = entregador.AdicionarContato(
            requisicao.Nome,
            requisicao.Telefone,
            requisicao.Parentesco);

        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);

        return ContatoResposta.De(contato);
    }

    /// <summary>
    /// Substitui os dados de um contato de emergência (mesmo corpo do <c>POST</c>).
    /// </summary>
    /// <param name="id">Id do entregador.</param>
    /// <param name="contatoId">Id do contato.</param>
    /// <param name="requisicao">Dados do contato.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O contato editado, com o telefone mascarado.</returns>
    /// <exception cref="RecursoNaoEncontradoException">Entregador inexistente, ou contato inexistente ou de outro entregador (404).</exception>
    /// <exception cref="AcessoNegadoException">Outro entregador ou papel errado (403).</exception>
    /// <exception cref="ValidacaoDominioException">Quando algum dado do contato é inválido (400).</exception>
    public async Task<ContatoResposta> EditarContatoAsync(
        Guid id,
        Guid contatoId,
        ContatoRequisicao requisicao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        Entregador entregador = await ObterDoDonoAsync(id, cancellationToken);

        ContatoEmergencia contato = entregador.EditarContato(
                contatoId,
                requisicao.Nome,
                requisicao.Telefone,
                requisicao.Parentesco)
            ?? throw ContatoNaoEncontrado();

        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);

        return ContatoResposta.De(contato);
    }

    /// <summary>
    /// Exclui um contato de emergência. Excluir o último é permitido.
    /// </summary>
    /// <param name="id">Id do entregador.</param>
    /// <param name="contatoId">Id do contato.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>A tarefa da exclusão.</returns>
    /// <exception cref="RecursoNaoEncontradoException">Entregador inexistente, ou contato inexistente ou de outro entregador (404).</exception>
    /// <exception cref="AcessoNegadoException">Outro entregador ou papel errado (403).</exception>
    public async Task RemoverContatoAsync(Guid id, Guid contatoId, CancellationToken cancellationToken)
    {
        Entregador entregador = await ObterDoDonoAsync(id, cancellationToken);

        if (!entregador.RemoverContato(contatoId))
        {
            throw ContatoNaoEncontrado();
        }

        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
    }

    // A conta do Identity grava pelo mesmo contexto da requisição; a transação faz a conta e o
    // entregador valerem juntos. Sem o ConfirmarAsync, o descarte da transação desfaz a conta.
    private async Task CriarContaVinculadaAsync(
        Entregador entregador,
        string email,
        string senha,
        bool novo,
        CancellationToken cancellationToken)
    {
        if (await _contas.EmailEmUsoAsync(email, cancellationToken))
        {
            throw new RegraNegocioException(IContasIdentity.MensagemEmailRepetido);
        }

        await using ITransacao transacao = await _unidadeDeTrabalho.IniciarTransacaoAsync(cancellationToken);

        // A conta vem primeiro: assim, o entregador ainda não está no contexto quando o Identity grava.
        string usuarioId = await _contas.CriarAsync(email, senha, Papeis.Entregador, cancellationToken);
        entregador.VincularUsuario(usuarioId);

        if (novo)
        {
            await _entregadores.AdicionarAsync(entregador, cancellationToken);
        }

        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
        await transacao.ConfirmarAsync(cancellationToken);
    }

    // 404 antes de 403: primeiro acha o entregador, depois confere se é o do token.
    private async Task<Entregador> ObterDoDonoAsync(Guid id, CancellationToken cancellationToken)
    {
        Entregador entregador = await _entregadores.ObterComCadastroAsync(id, cancellationToken)
            ?? throw EntregadorNaoEncontrado();

        _autorizacao.ExigirDono(entregador.Id);
        return entregador;
    }

    private async Task<EntregadorResposta> RespostaAsync(Entregador entregador, CancellationToken cancellationToken)
    {
        string? email = entregador.UsuarioId is { } usuarioId
            ? await _contas.ObterEmailAsync(usuarioId, cancellationToken)
            : null;

        return EntregadorResposta.De(entregador, email);
    }

    private static RecursoNaoEncontradoException EntregadorNaoEncontrado()
    {
        return new RecursoNaoEncontradoException("Entregador não encontrado.");
    }

    private static RecursoNaoEncontradoException ContatoNaoEncontrado()
    {
        return new RecursoNaoEncontradoException("Contato de emergência não encontrado.");
    }
}
