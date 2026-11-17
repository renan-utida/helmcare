using System.Threading;
using System.Threading.Tasks;

namespace HelmCare.Api.Seguranca;

/// <summary>
/// Contas de usuário (e-mail e senha) guardadas pelo ASP.NET Core Identity. Os serviços dependem
/// desta interface, e não do <c>UserManager</c>, para os testes usarem um fake sem banco.
/// </summary>
public interface IContasIdentity
{
    /// <summary>
    /// Mensagem do <c>409</c> de e-mail repetido.
    /// </summary>
    const string MensagemEmailRepetido = "Já existe uma conta com este e-mail.";

    /// <summary>
    /// Diz se o e-mail já é de alguma conta, sem diferenciar maiúsculas.
    /// </summary>
    /// <param name="email">E-mail.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns><see langword="true"/> se o e-mail já está em uso.</returns>
    Task<bool> EmailEmUsoAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Cria a conta com o papel. Grava no banco pelo mesmo contexto da requisição, então entra na
    /// transação aberta pelo serviço, se houver.
    /// </summary>
    /// <param name="email">E-mail (também usado como nome de usuário).</param>
    /// <param name="senha">Senha, guardada só como hash.</param>
    /// <param name="papel">Papel da conta (<see cref="Papeis"/>).</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O id do usuário criado.</returns>
    /// <exception cref="HelmCare.Dominio.Comum.ValidacaoDominioException">E-mail ou senha fora da regra (400).</exception>
    /// <exception cref="HelmCare.Dominio.Comum.RegraNegocioException">E-mail repetido (409).</exception>
    Task<string> CriarAsync(string email, string senha, string papel, CancellationToken cancellationToken);

    /// <summary>
    /// Confere a senha e conta a tentativa errada no bloqueio (5 erradas bloqueiam por 15 min).
    /// </summary>
    /// <param name="email">E-mail informado.</param>
    /// <param name="senha">Senha informada.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>A situação do login, o id do usuário (se o e-mail existe) e os papéis.</returns>
    Task<VerificacaoSenha> VerificarSenhaAsync(string email, string senha, CancellationToken cancellationToken);

    /// <summary>
    /// Busca o e-mail da conta, para a resposta mascarada do entregador.
    /// </summary>
    /// <param name="usuarioId">Id do usuário.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O e-mail, ou nulo se a conta não existe.</returns>
    Task<string?> ObterEmailAsync(string usuarioId, CancellationToken cancellationToken);
}
