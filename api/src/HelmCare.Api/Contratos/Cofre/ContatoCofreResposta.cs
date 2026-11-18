namespace HelmCare.Api.Contratos.Cofre;

/// <summary>
/// Contato de emergência na resposta do Cofre. Ao contrário das telas do entregador, o telefone vem
/// completo (só dígitos), porque o socorrista precisa ligar (seção 9 do contrato).
/// </summary>
public sealed class ContatoCofreResposta
{
    /// <summary>
    /// Nome do contato.
    /// </summary>
    public required string Nome { get; init; }

    /// <summary>
    /// Telefone completo, só com os dígitos (ex.: <c>11912345678</c>).
    /// </summary>
    public required string Telefone { get; init; }

    /// <summary>
    /// Relação com o entregador.
    /// </summary>
    public required string Parentesco { get; init; }
}
