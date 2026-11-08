using System;
using HelmCare.Dominio.Comum;

namespace HelmCare.Dominio.Entidades;

/// <summary>
/// Pessoa avisada quando o SOS do entregador é confirmado. Todos os campos são PII
/// (seção 9 do contrato). Só o <see cref="Entregador"/> cria contatos.
/// </summary>
public sealed class ContatoEmergencia : Entidade
{
    /// <summary>
    /// Tamanho máximo do nome.
    /// </summary>
    public const int TamanhoMaximoNome = 120;

    /// <summary>
    /// Tamanho máximo do parentesco.
    /// </summary>
    public const int TamanhoMaximoParentesco = 40;

    /// <summary>
    /// Cria o contato validando os campos. Interno: chamado por <see cref="Entregador.AdicionarContato"/>.
    /// </summary>
    /// <param name="entregadorId">Entregador dono do contato.</param>
    /// <param name="nome">Nome do contato.</param>
    /// <param name="telefone">Telefone com DDD (10 ou 11 dígitos).</param>
    /// <param name="parentesco">Relação com o entregador.</param>
    internal ContatoEmergencia(Guid entregadorId, string nome, string telefone, string parentesco)
    {
        EntregadorId = Garantir.IdInformado(entregadorId, "entregadorId");
        Nome = Garantir.TextoObrigatorio(nome, "nome", TamanhoMaximoNome);
        Telefone = Garantir.SoDigitos(telefone, "telefone", 10, 11);
        Parentesco = Garantir.TextoObrigatorio(parentesco, "parentesco", TamanhoMaximoParentesco);
    }

    // Construtor usado só pelo EF Core ao materializar o registro do banco.
    private ContatoEmergencia()
    {
        Nome = string.Empty;
        Telefone = string.Empty;
        Parentesco = string.Empty;
    }

    /// <summary>
    /// Entregador dono do contato.
    /// </summary>
    public Guid EntregadorId { get; private set; }

    /// <summary>
    /// Nome do contato (PII).
    /// </summary>
    public string Nome { get; private set; }

    /// <summary>
    /// Telefone com DDD, só dígitos (PII).
    /// </summary>
    public string Telefone { get; private set; }

    /// <summary>
    /// Relação com o entregador (ex.: mãe, irmão, cônjuge).
    /// </summary>
    public string Parentesco { get; private set; }
}
