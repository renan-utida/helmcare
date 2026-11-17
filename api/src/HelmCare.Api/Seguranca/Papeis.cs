namespace HelmCare.Api.Seguranca;

/// <summary>
/// Papéis do ASP.NET Core Identity (seção 7 do contrato, "Identidade e autorização"). Os dois são
/// criados pela migration <c>Identidade</c>, com ids fixos, para o banco de desenvolvimento e o da VM
/// terem os mesmos registros. O voluntário não é um papel: é um entregador, com o mesmo token.
/// </summary>
public static class Papeis
{
    /// <summary>
    /// Entregador (e voluntário): usa o app e as rotas do próprio cadastro, turno e ocorrência.
    /// </summary>
    public const string Entregador = "ENTREGADOR";

    /// <summary>
    /// Socorrista: só usa o Cofre médico (rodada 2). É recusado nas rotas do entregador.
    /// </summary>
    public const string Socorrista = "SOCORRISTA";

    /// <summary>
    /// Id fixo do papel <see cref="Entregador"/> na tabela <c>AspNetRoles</c>.
    /// </summary>
    public const string IdEntregador = "3b0f1c52-7a4e-4c1d-9e2f-5a6b7c8d9e01";

    /// <summary>
    /// Id fixo do papel <see cref="Socorrista"/> na tabela <c>AspNetRoles</c>.
    /// </summary>
    public const string IdSocorrista = "3b0f1c52-7a4e-4c1d-9e2f-5a6b7c8d9e02";
}
