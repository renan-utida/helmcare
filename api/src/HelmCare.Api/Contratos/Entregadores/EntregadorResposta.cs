using System;
using System.Collections.Generic;
using System.Linq;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Api.Contratos.Entregadores;

/// <summary>
/// Dados do entregador devolvidos por <c>POST /api/v1/entregadores</c> e
/// <c>GET /api/v1/entregadores/{id}</c>, com a PII mascarada (seção 9 do contrato).
/// </summary>
/// <remarks>
/// Esta resposta NUNCA leva o conteúdo da ficha médica, só <see cref="PossuiFichaMedica"/>.
/// As rotas do entregador não têm login no protótipo: qualquer pessoa com o id leria dados de
/// saúde, que pela seção 9 só podem sair para o socorrista verificado no Cofre.
/// </remarks>
public sealed class EntregadorResposta
{
    /// <summary>
    /// Id do entregador.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Nome completo.
    /// </summary>
    public required string Nome { get; init; }

    /// <summary>
    /// CPF mascarado: <c>***.***.***-12</c>.
    /// </summary>
    public required string Cpf { get; init; }

    /// <summary>
    /// CNH mascarada: <c>*******1234</c>.
    /// </summary>
    public required string Cnh { get; init; }

    /// <summary>
    /// Telefone mascarado: <c>(11) *****-1234</c>.
    /// </summary>
    public required string Telefone { get; init; }

    /// <summary>
    /// Instante do cadastro, em UTC.
    /// </summary>
    public required DateTime CriadoEm { get; init; }

    /// <summary>
    /// Diz se a ficha médica já foi preenchida, sem mostrar o conteúdo.
    /// </summary>
    public required bool PossuiFichaMedica { get; init; }

    /// <summary>
    /// Contatos de emergência, com o telefone mascarado.
    /// </summary>
    public required IReadOnlyList<ContatoResposta> Contatos { get; init; }

    /// <summary>
    /// Monta a resposta a partir da entidade, aplicando as máscaras.
    /// </summary>
    /// <param name="entregador">Entregador, com os contatos e a ficha carregados.</param>
    /// <returns>A resposta mascarada.</returns>
    public static EntregadorResposta De(Entregador entregador)
    {
        ArgumentNullException.ThrowIfNull(entregador);

        return new EntregadorResposta
        {
            Id = entregador.Id,
            Nome = entregador.Nome,
            Cpf = Mascaras.Cpf(entregador.Cpf),
            Cnh = Mascaras.Cnh(entregador.Cnh),
            Telefone = Mascaras.Telefone(entregador.Telefone),
            CriadoEm = entregador.CriadoEm,
            PossuiFichaMedica = entregador.FichaMedica is not null,
            Contatos = entregador.Contatos.Select(ContatoResposta.De).ToList(),
        };
    }
}
