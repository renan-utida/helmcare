using System.ComponentModel.DataAnnotations;
using HelmCare.Api.Contratos.Entregadores;
using HelmCare.Api.Contratos.Validacao;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Api.Contratos.Socorristas;

/// <summary>
/// Corpo do <c>POST /api/v1/dev/socorristas</c> (só em Development): o socorrista da demonstração e a
/// conta dele, com as regras de e-mail e senha do cadastro do entregador.
/// </summary>
public sealed class CriarSocorristaRequisicao
{
    /// <summary>
    /// Nome do socorrista, até 120 caracteres.
    /// </summary>
    /// <example>Socorrista Simulado</example>
    [Required(ErrorMessage = "O campo nome é obrigatório.")]
    [StringLength(Socorrista.TamanhoMaximoNome, ErrorMessage = "O campo nome deve ter até 120 caracteres.")]
    public string Nome { get; init; } = string.Empty;

    /// <summary>
    /// Registro profissional (ex.: COREN), até 40 caracteres.
    /// </summary>
    /// <example>COREN-SP 000000</example>
    [Required(ErrorMessage = "O campo registroProfissional é obrigatório.")]
    [StringLength(Socorrista.TamanhoMaximoRegistro, ErrorMessage = "O campo registroProfissional deve ter até 40 caracteres.")]
    public string RegistroProfissional { get; init; } = string.Empty;

    /// <summary>
    /// Organização em que atua (ex.: SAMU), até 120 caracteres.
    /// </summary>
    /// <example>SAMU Simulado</example>
    [Required(ErrorMessage = "O campo organizacao é obrigatório.")]
    [StringLength(Socorrista.TamanhoMaximoOrganizacao, ErrorMessage = "O campo organizacao deve ter até 120 caracteres.")]
    public string Organizacao { get; init; } = string.Empty;

    /// <summary>
    /// Se o socorrista já nasce verificado (na demonstração, um verificado e um não verificado).
    /// </summary>
    public bool Verificado { get; init; }

    /// <summary>
    /// E-mail da conta, até 254 caracteres, sem repetir.
    /// </summary>
    /// <example>socorrista@exemplo.com</example>
    [Required(ErrorMessage = "O campo email é obrigatório.")]
    [EmailAddress(ErrorMessage = "O campo email deve ser um e-mail válido.")]
    [StringLength(CriarEntregadorRequisicao.TamanhoMaximoEmail, ErrorMessage = "O campo email deve ter até 254 caracteres.")]
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Senha de 8 a 64 caracteres, com maiúscula, minúscula e dígito.
    /// </summary>
    /// <example>Senha123</example>
    [Required(ErrorMessage = "O campo senha é obrigatório.")]
    [Senha]
    public string Senha { get; init; } = string.Empty;
}
