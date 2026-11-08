using System;
using System.Collections.Frozen;
using HelmCare.Dominio.Comum;

namespace HelmCare.Dominio.Entidades;

/// <summary>
/// Ficha médica do entregador, lida pelo socorrista no Cofre durante uma emergência.
/// <b>PII sensível (saúde)</b>, seção 9 do contrato: todos os campos só saem na resposta de
/// <c>POST /api/v1/cofre/{qrToken}/acesso</c>, para socorrista verificado e dentro do TTL.
/// Nunca vão para logs, MQTT, notificações ou respostas para voluntários.
/// Só o <see cref="Entregador"/> cria e altera a ficha.
/// </summary>
public sealed class FichaMedica : Entidade
{
    /// <summary>
    /// Tamanho máximo de cada campo de texto livre (alergias, condições e medicamentos).
    /// </summary>
    public const int TamanhoMaximoTexto = 500;

    /// <summary>
    /// Tamanho máximo do tipo sanguíneo (ex.: <c>AB+</c>).
    /// </summary>
    public const int TamanhoMaximoTipoSanguineo = 3;

    private static readonly FrozenSet<string> _tiposSanguineos =
        FrozenSet.Create(StringComparer.Ordinal, "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-");

    /// <summary>
    /// Cria a ficha validando os campos. Interno: chamado por <see cref="Entregador.DefinirFichaMedica"/>.
    /// </summary>
    /// <param name="entregadorId">Entregador dono da ficha.</param>
    /// <param name="tipoSanguineo">Tipo sanguíneo.</param>
    /// <param name="alergias">Alergias, opcional.</param>
    /// <param name="condicoesCriticas">Condições críticas, opcional.</param>
    /// <param name="medicamentos">Medicamentos, opcional.</param>
    internal FichaMedica(
        Guid entregadorId,
        string tipoSanguineo,
        string? alergias,
        string? condicoesCriticas,
        string? medicamentos)
    {
        EntregadorId = Garantir.IdInformado(entregadorId, "entregadorId");
        TipoSanguineo = string.Empty;
        Alergias = string.Empty;
        CondicoesCriticas = string.Empty;
        Medicamentos = string.Empty;
        Atualizar(tipoSanguineo, alergias, condicoesCriticas, medicamentos);
    }

    // Construtor usado só pelo EF Core ao materializar o registro do banco.
    private FichaMedica()
    {
        TipoSanguineo = string.Empty;
        Alergias = string.Empty;
        CondicoesCriticas = string.Empty;
        Medicamentos = string.Empty;
    }

    /// <summary>
    /// Entregador dono da ficha.
    /// </summary>
    public Guid EntregadorId { get; private set; }

    /// <summary>
    /// Tipo sanguíneo: A+, A-, B+, B-, AB+, AB-, O+ ou O-.
    /// </summary>
    public string TipoSanguineo { get; private set; }

    /// <summary>
    /// Alergias conhecidas (vazio quando não informado).
    /// </summary>
    public string Alergias { get; private set; }

    /// <summary>
    /// Condições críticas de saúde, como diabetes ou epilepsia (vazio quando não informado).
    /// </summary>
    public string CondicoesCriticas { get; private set; }

    /// <summary>
    /// Medicamentos de uso contínuo (vazio quando não informado).
    /// </summary>
    public string Medicamentos { get; private set; }

    /// <summary>
    /// Substitui todos os dados da ficha. Valida tudo antes de alterar qualquer campo,
    /// para a ficha nunca ficar pela metade.
    /// </summary>
    /// <param name="tipoSanguineo">Tipo sanguíneo.</param>
    /// <param name="alergias">Alergias, opcional.</param>
    /// <param name="condicoesCriticas">Condições críticas, opcional.</param>
    /// <param name="medicamentos">Medicamentos, opcional.</param>
    /// <exception cref="ValidacaoDominioException">Quando algum dado é inválido.</exception>
    internal void Atualizar(string tipoSanguineo, string? alergias, string? condicoesCriticas, string? medicamentos)
    {
        string tipo = Garantir.TextoObrigatorio(tipoSanguineo, "tipoSanguineo", TamanhoMaximoTipoSanguineo);

        if (!_tiposSanguineos.Contains(tipo))
        {
            throw new ValidacaoDominioException(
                "O campo tipoSanguineo deve ser A+, A-, B+, B-, AB+, AB-, O+ ou O-.");
        }

        string alergiasValidadas = Garantir.TextoOpcional(alergias, "alergias", TamanhoMaximoTexto);
        string condicoesValidadas = Garantir.TextoOpcional(condicoesCriticas, "condicoesCriticas", TamanhoMaximoTexto);
        string medicamentosValidados = Garantir.TextoOpcional(medicamentos, "medicamentos", TamanhoMaximoTexto);

        TipoSanguineo = tipo;
        Alergias = alergiasValidadas;
        CondicoesCriticas = condicoesValidadas;
        Medicamentos = medicamentosValidados;
    }
}
