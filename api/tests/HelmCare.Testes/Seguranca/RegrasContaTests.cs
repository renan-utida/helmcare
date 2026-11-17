using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Contratos;
using HelmCare.Api.Contratos.Entregadores;
using HelmCare.Api.Contratos.Validacao;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Seguranca;

/// <summary>
/// Testes das regras de conta da seção 3 do contrato (1.15): senha, e-mail, máscara do e-mail,
/// regras do Identity e as faixas das opções novas.
/// </summary>
[TestClass]
public sealed class RegrasContaTests
{
    /// <summary>
    /// Garante a regra da senha: de 8 a 64 caracteres, com maiúscula, minúscula e dígito, sem símbolo.
    /// </summary>
    /// <param name="senha">Senha testada.</param>
    /// <param name="valida">Se a regra aceita.</param>
    [TestMethod]
    [DataRow("Senha123", true)]
    [DataRow("Abcdefg1", true)]
    [DataRow("Senha12", false)]
    [DataRow("senha123", false)]
    [DataRow("SENHA123", false)]
    [DataRow("SenhaSemDigito", false)]
    [DataRow("Senha123!@#", true)]
    public void Senha_Regra_AceitaSoOQueOContratoPede(string senha, bool valida)
    {
        // Arrange
        var regra = new SenhaAttribute();

        // Act
        bool resultado = regra.IsValid(senha);

        // Assert
        Assert.AreEqual(valida, resultado);
    }

    /// <summary>
    /// Garante os limites de tamanho da senha: 64 passa, 65 não.
    /// </summary>
    /// <param name="tamanho">Tamanho da senha.</param>
    /// <param name="valida">Se a regra aceita.</param>
    [TestMethod]
    [DataRow(64, true)]
    [DataRow(65, false)]
    public void Senha_Tamanho_MaximoDe64(int tamanho, bool valida)
    {
        // Arrange
        string senha = "Aa1" + new string('x', tamanho - 3);

        // Act
        bool resultado = new SenhaAttribute().IsValid(senha);

        // Assert
        Assert.AreEqual(valida, resultado);
    }

    /// <summary>
    /// Garante as validações do cadastro para o e-mail e a senha (400 automático do [ApiController]).
    /// </summary>
    /// <param name="email">E-mail enviado.</param>
    /// <param name="senha">Senha enviada.</param>
    /// <param name="campoComErro">Campo que deve falhar, ou vazio quando tudo passa.</param>
    [TestMethod]
    [DataRow("entregador@exemplo.com", "Senha123", "")]
    [DataRow("sem-arroba.exemplo.com", "Senha123", "Email")]
    [DataRow("", "Senha123", "Email")]
    [DataRow("entregador@exemplo.com", "fraca", "Senha")]
    public void Cadastro_EmailESenha_ValidaPelasRegras(string email, string senha, string campoComErro)
    {
        // Arrange
        var requisicao = new CriarEntregadorRequisicao
        {
            Nome = "Entregador Simulado",
            Cpf = "111.444.777-35",
            Cnh = "12345678900",
            Telefone = "(11) 91234-5678",
            Email = email,
            Senha = senha,
        };

        // Act
        List<string> campos = Erros(requisicao);

        // Assert
        if (campoComErro.Length == 0)
        {
            Assert.IsEmpty(campos);
        }
        else
        {
            Assert.AreEqual(campoComErro, campos.Single());
        }
    }

    /// <summary>
    /// Garante o limite de 254 caracteres do e-mail.
    /// </summary>
    [TestMethod]
    public void Cadastro_EmailCom255Caracteres_Reprovado()
    {
        // Arrange
        string email = new string('a', 255 - "@exemplo.com".Length) + "@exemplo.com";
        var requisicao = new ContaRequisicao { Email = email, Senha = "Senha123" };

        // Act
        List<string> campos = Erros(requisicao);

        // Assert
        Assert.AreEqual("Email", campos.Single());
    }

    /// <summary>
    /// Garante a máscara do e-mail: a primeira letra, *** e o domínio.
    /// </summary>
    /// <param name="email">E-mail.</param>
    /// <param name="esperado">E-mail mascarado.</param>
    [TestMethod]
    [DataRow("renan@exemplo.com", "r***@exemplo.com")]
    [DataRow("a@exemplo.com", "a***@exemplo.com")]
    [DataRow("Entregador.Teste@sub.exemplo.com", "E***@sub.exemplo.com")]
    public void Email_Mascara_PrimeiraLetraEODominio(string email, string esperado)
    {
        // Act
        string mascarado = Mascaras.Email(email);

        // Assert
        Assert.AreEqual(esperado, mascarado);
    }

    /// <summary>
    /// Garante que um valor sem a forma de e-mail é recusado sem repetir o valor na mensagem.
    /// </summary>
    /// <param name="valor">Valor inválido.</param>
    [TestMethod]
    [DataRow("@exemplo.com")]
    [DataRow("sem-arroba")]
    [DataRow("fim@")]
    public void Email_SemFormato_LancaSemRepetirOValor(string valor)
    {
        // Act
        ArgumentException excecao = Assert.ThrowsExactly<ArgumentException>(() => Mascaras.Email(valor));

        // Assert
        Assert.DoesNotContain(valor, excecao.Message);
    }

    /// <summary>
    /// Garante as regras do Identity: senha sem símbolo obrigatório, e-mail único e bloqueio de 15 min
    /// depois de 5 tentativas erradas, também nas contas novas.
    /// </summary>
    [TestMethod]
    public void ConfigurarIdentity_Regras_SaoAsDoContrato()
    {
        // Arrange
        var opcoes = new IdentityOptions();

        // Act
        ConfiguracaoIdentidade.ConfigurarIdentity(opcoes);

        // Assert
        Assert.AreEqual(8, opcoes.Password.RequiredLength);
        Assert.IsTrue(opcoes.Password.RequireUppercase);
        Assert.IsTrue(opcoes.Password.RequireLowercase);
        Assert.IsTrue(opcoes.Password.RequireDigit);
        Assert.IsFalse(opcoes.Password.RequireNonAlphanumeric);
        Assert.IsTrue(opcoes.User.RequireUniqueEmail);
        Assert.AreEqual(5, opcoes.Lockout.MaxFailedAccessAttempts);
        Assert.AreEqual(TimeSpan.FromMinutes(15), opcoes.Lockout.DefaultLockoutTimeSpan);
        Assert.IsTrue(opcoes.Lockout.AllowedForNewUsers);
    }

    /// <summary>
    /// Garante os valores padrão das opções novas: tokens de 60 min e 14 dias, 120 min de encerramento
    /// automático, 10 logins por minuto e o loopback como proxy confiável.
    /// </summary>
    [TestMethod]
    public void Opcoes_Padrao_SaoAsDoContrato()
    {
        // Arrange e Act
        var opcoes = new OpcoesHelmCare();

        // Assert
        Assert.AreEqual(60, opcoes.ValidadeAccessTokenMin);
        Assert.AreEqual(14, opcoes.ValidadeRefreshTokenDias);
        Assert.AreEqual(120, opcoes.TempoEncerramentoAutomaticoMin);
        Assert.AreEqual(TimeSpan.FromMinutes(120), opcoes.TempoEncerramentoAutomatico);
        Assert.AreEqual(10, opcoes.LimiteLoginsPorMinuto);
        Assert.AreEqual("127.0.0.1,::1", opcoes.ProxiesConfiaveis);
        Assert.IsNull(opcoes.DiretorioChavesDataProtection);
    }

    /// <summary>
    /// Garante as faixas das validades dos tokens e do tempo de encerramento automático (a API não sobe fora delas).
    /// </summary>
    /// <param name="campo">Opção testada.</param>
    /// <param name="valor">Valor configurado.</param>
    /// <param name="aceito">Se a validação aprova.</param>
    [TestMethod]
    [DataRow(nameof(OpcoesHelmCare.ValidadeAccessTokenMin), 5, true)]
    [DataRow(nameof(OpcoesHelmCare.ValidadeAccessTokenMin), 1440, true)]
    [DataRow(nameof(OpcoesHelmCare.ValidadeAccessTokenMin), 4, false)]
    [DataRow(nameof(OpcoesHelmCare.ValidadeAccessTokenMin), 1441, false)]
    [DataRow(nameof(OpcoesHelmCare.ValidadeRefreshTokenDias), 1, true)]
    [DataRow(nameof(OpcoesHelmCare.ValidadeRefreshTokenDias), 90, true)]
    [DataRow(nameof(OpcoesHelmCare.ValidadeRefreshTokenDias), 0, false)]
    [DataRow(nameof(OpcoesHelmCare.ValidadeRefreshTokenDias), 91, false)]
    [DataRow(nameof(OpcoesHelmCare.TempoEncerramentoAutomaticoMin), 10, true)]
    [DataRow(nameof(OpcoesHelmCare.TempoEncerramentoAutomaticoMin), 1440, true)]
    [DataRow(nameof(OpcoesHelmCare.TempoEncerramentoAutomaticoMin), 9, false)]
    [DataRow(nameof(OpcoesHelmCare.TempoEncerramentoAutomaticoMin), 1441, false)]
    [DataRow(nameof(OpcoesHelmCare.LimiteLoginsPorMinuto), 0, false)]
    public void Validacao_OpcoesNovas_RespeitamAFaixa(string campo, int valor, bool aceito)
    {
        // Arrange
        var servicos = new ServiceCollection();
        servicos.AddOptions<OpcoesHelmCare>()
            .Configure(o => typeof(OpcoesHelmCare).GetProperty(campo)!.SetValue(o, valor))
            .ValidateDataAnnotations();
        using ServiceProvider provedor = servicos.BuildServiceProvider();
        IOptions<OpcoesHelmCare> opcoes = provedor.GetRequiredService<IOptions<OpcoesHelmCare>>();

        // Act
        Exception? erro = null;
        try
        {
            _ = opcoes.Value;
        }
        catch (OptionsValidationException ex)
        {
            erro = ex;
        }

        // Assert
        Assert.AreEqual(aceito, erro is null);
    }

    private static List<string> Erros(object requisicao)
    {
        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(requisicao, new ValidationContext(requisicao), resultados, validateAllProperties: true);
        return resultados.SelectMany(r => r.MemberNames).ToList();
    }
}
