using System;
using System.Collections.Generic;
using System.Linq;
using HelmCare.Dominio.Comum;

namespace HelmCare.Dominio.Entidades;

/// <summary>
/// Entregador de moto protegido pelo HelmCare. É a raiz do cadastro: a ficha médica e os
/// contatos de emergência só são criados e alterados por meio dele.
/// <c>Nome</c>, <c>Cpf</c>, <c>Cnh</c> e <c>Telefone</c> são PII (seção 9 do contrato):
/// aparecem só nas telas do próprio entregador, nunca em logs, MQTT ou respostas para voluntários.
/// </summary>
public sealed class Entregador : Entidade
{
    /// <summary>
    /// Tamanho máximo do nome.
    /// </summary>
    public const int TamanhoMaximoNome = 120;

    /// <summary>
    /// Tamanho máximo do id do usuário do Identity (o mesmo do <see cref="Socorrista.TamanhoMaximoUsuarioId"/>).
    /// </summary>
    public const int TamanhoMaximoUsuarioId = 450;

    private readonly List<ContatoEmergencia> _contatos = [];

    /// <summary>
    /// Cria um entregador validando e normalizando os documentos.
    /// </summary>
    /// <param name="nome">Nome completo, até 120 caracteres.</param>
    /// <param name="cpf">CPF com ou sem máscara (11 dígitos).</param>
    /// <param name="cnh">Número da CNH com ou sem máscara (11 dígitos).</param>
    /// <param name="telefone">Telefone com DDD, com ou sem máscara (10 ou 11 dígitos).</param>
    /// <param name="criadoEm">Instante do cadastro, em UTC.</param>
    /// <exception cref="ValidacaoDominioException">Quando algum dado é inválido.</exception>
    public Entregador(string nome, string cpf, string cnh, string telefone, DateTime criadoEm)
    {
        Nome = Garantir.TextoObrigatorio(nome, "nome", TamanhoMaximoNome);
        Cpf = Garantir.SoDigitos(cpf, "cpf", 11);
        Cnh = Garantir.SoDigitos(cnh, "cnh", 11);
        Telefone = Garantir.SoDigitos(telefone, "telefone", 10, 11);
        CriadoEm = Garantir.Utc(criadoEm, "criadoEm");
    }

    // Construtor usado só pelo EF Core ao materializar o registro do banco.
    private Entregador()
    {
        Nome = string.Empty;
        Cpf = string.Empty;
        Cnh = string.Empty;
        Telefone = string.Empty;
    }

    /// <summary>
    /// Nome completo do entregador (PII).
    /// </summary>
    public string Nome { get; private set; }

    /// <summary>
    /// CPF guardado só com os 11 dígitos (PII). Nas respostas sai mascarado: <c>***.***.***-12</c>.
    /// </summary>
    public string Cpf { get; private set; }

    /// <summary>
    /// Número da CNH guardado só com os 11 dígitos (PII).
    /// </summary>
    public string Cnh { get; private set; }

    /// <summary>
    /// Telefone com DDD, só dígitos (PII). Nas respostas sai mascarado: <c>(11) *****-1234</c>.
    /// </summary>
    public string Telefone { get; private set; }

    /// <summary>
    /// Instante do cadastro, em UTC.
    /// </summary>
    public DateTime CriadoEm { get; private set; }

    /// <summary>
    /// Ficha médica do entregador, ou nulo enquanto não for preenchida.
    /// </summary>
    public FichaMedica? FichaMedica { get; private set; }

    /// <summary>
    /// Contatos de emergência, avisados quando o SOS é confirmado.
    /// </summary>
    public IReadOnlyCollection<ContatoEmergencia> Contatos => _contatos.AsReadOnly();

    /// <summary>
    /// Id do usuário do ASP.NET Core Identity (e-mail e senha) usado no login, ou nulo para os
    /// entregadores cadastrados antes da 1.15, que ainda não têm conta.
    /// </summary>
    public string? UsuarioId { get; private set; }

    /// <summary>
    /// Vincula o entregador a um usuário do Identity. Repetir com o mesmo usuário não muda nada.
    /// </summary>
    /// <param name="usuarioId">Id do usuário do Identity.</param>
    /// <exception cref="ValidacaoDominioException">Quando o id é vazio ou longo demais.</exception>
    /// <exception cref="RegraNegocioException">Quando o entregador já tem outra conta.</exception>
    public void VincularUsuario(string usuarioId)
    {
        string id = Garantir.TextoObrigatorio(usuarioId, "usuarioId", TamanhoMaximoUsuarioId);

        // Trocar a conta vinculada daria os dados do entregador para outro login.
        if (UsuarioId is not null && UsuarioId != id)
        {
            throw new RegraNegocioException("O entregador já tem uma conta.");
        }

        UsuarioId = id;
    }

    /// <summary>
    /// Troca o telefone do entregador, com a mesma regra do cadastro.
    /// </summary>
    /// <param name="telefone">Telefone com DDD, com ou sem máscara (10 ou 11 dígitos).</param>
    /// <exception cref="ValidacaoDominioException">Quando o telefone é inválido.</exception>
    public void TrocarTelefone(string telefone)
    {
        Telefone = Garantir.SoDigitos(telefone, "telefone", 10, 11);
    }

    /// <summary>
    /// Cria a ficha médica na primeira chamada e atualiza a mesma instância nas seguintes,
    /// garantindo uma única ficha por entregador.
    /// </summary>
    /// <param name="tipoSanguineo">Tipo sanguíneo (A+, A-, B+, B-, AB+, AB-, O+ ou O-).</param>
    /// <param name="alergias">Alergias, opcional.</param>
    /// <param name="condicoesCriticas">Condições críticas de saúde, opcional.</param>
    /// <param name="medicamentos">Medicamentos de uso contínuo, opcional.</param>
    /// <returns>A ficha médica criada ou atualizada.</returns>
    /// <exception cref="ValidacaoDominioException">Quando algum dado da ficha é inválido.</exception>
    public FichaMedica DefinirFichaMedica(
        string tipoSanguineo,
        string? alergias,
        string? condicoesCriticas,
        string? medicamentos)
    {
        if (FichaMedica is null)
        {
            FichaMedica = new FichaMedica(Id, tipoSanguineo, alergias, condicoesCriticas, medicamentos);
        }
        else
        {
            FichaMedica.Atualizar(tipoSanguineo, alergias, condicoesCriticas, medicamentos);
        }

        return FichaMedica;
    }

    /// <summary>
    /// Adiciona um contato de emergência ao entregador.
    /// </summary>
    /// <param name="nome">Nome do contato, até 120 caracteres.</param>
    /// <param name="telefone">Telefone com DDD (10 ou 11 dígitos).</param>
    /// <param name="parentesco">Relação com o entregador (ex.: mãe), até 40 caracteres.</param>
    /// <returns>O contato criado.</returns>
    /// <exception cref="ValidacaoDominioException">Quando algum dado do contato é inválido.</exception>
    public ContatoEmergencia AdicionarContato(string nome, string telefone, string parentesco)
    {
        var contato = new ContatoEmergencia(Id, nome, telefone, parentesco);
        _contatos.Add(contato);
        return contato;
    }

    /// <summary>
    /// Substitui os dados de um contato de emergência do entregador.
    /// </summary>
    /// <param name="contatoId">Id do contato.</param>
    /// <param name="nome">Nome do contato, até 120 caracteres.</param>
    /// <param name="telefone">Telefone com DDD (10 ou 11 dígitos).</param>
    /// <param name="parentesco">Relação com o entregador, até 40 caracteres.</param>
    /// <returns>O contato editado, ou nulo se o entregador não tem contato com esse id.</returns>
    /// <exception cref="ValidacaoDominioException">Quando algum dado do contato é inválido.</exception>
    public ContatoEmergencia? EditarContato(Guid contatoId, string nome, string telefone, string parentesco)
    {
        ContatoEmergencia? contato = _contatos.FirstOrDefault(c => c.Id == contatoId);
        contato?.Atualizar(nome, telefone, parentesco);
        return contato;
    }

    /// <summary>
    /// Exclui um contato de emergência do entregador. Excluir o último contato é permitido.
    /// </summary>
    /// <param name="contatoId">Id do contato.</param>
    /// <returns><see langword="true"/> se o contato existia e foi excluído.</returns>
    public bool RemoverContato(Guid contatoId)
    {
        return _contatos.RemoveAll(c => c.Id == contatoId) > 0;
    }
}
