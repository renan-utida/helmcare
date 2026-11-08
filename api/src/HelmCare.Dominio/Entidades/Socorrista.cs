using HelmCare.Dominio.Comum;

namespace HelmCare.Dominio.Entidades;

/// <summary>
/// Profissional de saúde ou resgate com acesso ao Cofre médico. Só socorristas verificados
/// leem a ficha médica, e o login é feito pelo ASP.NET Core Identity.
/// </summary>
public sealed class Socorrista : Entidade
{
    /// <summary>
    /// Tamanho máximo do nome.
    /// </summary>
    public const int TamanhoMaximoNome = 120;

    /// <summary>
    /// Tamanho máximo do registro profissional.
    /// </summary>
    public const int TamanhoMaximoRegistro = 40;

    /// <summary>
    /// Tamanho máximo da organização.
    /// </summary>
    public const int TamanhoMaximoOrganizacao = 120;

    /// <summary>
    /// Tamanho máximo do id do usuário do Identity (o mesmo da coluna <c>AspNetUsers.Id</c>).
    /// </summary>
    public const int TamanhoMaximoUsuarioId = 450;

    /// <summary>
    /// Cadastra o socorrista ainda não verificado e sem usuário vinculado.
    /// </summary>
    /// <param name="nome">Nome completo.</param>
    /// <param name="registroProfissional">Registro profissional (ex.: COREN, CRM).</param>
    /// <param name="organizacao">Organização em que atua (ex.: SAMU).</param>
    /// <exception cref="ValidacaoDominioException">Quando algum dado é inválido.</exception>
    public Socorrista(string nome, string registroProfissional, string organizacao)
    {
        Nome = Garantir.TextoObrigatorio(nome, "nome", TamanhoMaximoNome);
        RegistroProfissional = Garantir.TextoObrigatorio(registroProfissional, "registroProfissional", TamanhoMaximoRegistro);
        Organizacao = Garantir.TextoObrigatorio(organizacao, "organizacao", TamanhoMaximoOrganizacao);
        Verificado = false;
    }

    // Construtor usado só pelo EF Core ao materializar o registro do banco.
    private Socorrista()
    {
        Nome = string.Empty;
        RegistroProfissional = string.Empty;
        Organizacao = string.Empty;
    }

    /// <summary>
    /// Nome completo do socorrista.
    /// </summary>
    public string Nome { get; private set; }

    /// <summary>
    /// Registro profissional (ex.: COREN, CRM).
    /// </summary>
    public string RegistroProfissional { get; private set; }

    /// <summary>
    /// Organização em que atua.
    /// </summary>
    public string Organizacao { get; private set; }

    /// <summary>
    /// Indica se o registro foi conferido. Sem isso o socorrista não acessa o Cofre.
    /// </summary>
    public bool Verificado { get; private set; }

    /// <summary>
    /// Id do usuário do ASP.NET Core Identity usado no login, ou nulo se ainda não vinculado.
    /// </summary>
    public string? UsuarioId { get; private set; }

    /// <summary>
    /// Marca o socorrista como verificado. Chamar de novo não muda nada.
    /// </summary>
    public void Verificar()
    {
        Verificado = true;
    }

    /// <summary>
    /// Vincula o socorrista a um usuário do Identity. Repetir com o mesmo usuário não muda nada.
    /// </summary>
    /// <param name="usuarioId">Id do usuário do Identity.</param>
    /// <exception cref="ValidacaoDominioException">Quando o id é vazio ou longo demais.</exception>
    /// <exception cref="RegraNegocioException">Quando o socorrista já está vinculado a outro usuário.</exception>
    public void VincularUsuario(string usuarioId)
    {
        string id = Garantir.TextoObrigatorio(usuarioId, "usuarioId", TamanhoMaximoUsuarioId);

        // Trocar o usuário vinculado daria o acesso ao Cofre para outra conta.
        if (UsuarioId is not null && UsuarioId != id)
        {
            throw new RegraNegocioException("O socorrista já está vinculado a outro usuário.");
        }

        UsuarioId = id;
    }
}
