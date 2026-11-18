using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Contratos.Cofre;
using HelmCare.Api.Seguranca;
using HelmCare.Api.Servicos.Cofre;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using HelmCare.Testes.Construtores;
using HelmCare.Testes.Fakes;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Servicos;

/// <summary>
/// Testes do <see cref="CofreServico"/> com a autorização real: a ordem dos códigos, a auditoria de cada
/// tentativa, o que não gera auditoria, o corpo da resposta e a auditoria do entregador.
/// </summary>
[TestClass]
public sealed class CofreServicoTests
{
    private const string Ip = "203.0.113.7";

    private readonly FakeDispositivoRepositorio _dispositivos = new();
    private readonly FakeSocorristaRepositorio _socorristas = new();
    private readonly FakeEntregadorRepositorio _entregadores = new();
    private readonly FakeOcorrenciaRepositorio _ocorrencias = new();
    private readonly FakeVoluntarioRepositorio _voluntarios = new();
    private readonly FakeUnidadeDeTrabalho _unidade = new();
    private readonly FakeAcessoCofreRepositorio _acessos;
    private readonly FakeLogger<CofreServico> _logger = new();
    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(CenarioSos.Sos.AddMinutes(1)));
    private readonly Entregador _entregador = CenarioSos.NovoEntregador();
    private readonly Dispositivo _capacete;
    private readonly Socorrista _verificado = new("Socorrista Simulado", "COREN-SP 000001", "SAMU Simulado");
    private readonly Socorrista _naoVerificado = new("Socorrista Pendente", "COREN-SP 000002", "SAMU Simulado");

    /// <summary>
    /// Semeia o entregador com ficha (inclusive medicamentos) e um contato, o capacete e os dois socorristas.
    /// </summary>
    public CofreServicoTests()
    {
        _acessos = new FakeAcessoCofreRepositorio(_socorristas);
        _entregador.DefinirFichaMedica("O+", "Dipirona", "Asma", "Salbutamol");
        _entregador.AdicionarContato("Contato Simulado", "11912345678", "Mãe");
        _entregadores.Semear(_entregador);
        _capacete = new Dispositivo("HC-0001", _entregador.Id);
        _dispositivos.Semear(_capacete);
        _verificado.Verificar();
        _socorristas.Semear(_verificado);
        _socorristas.Semear(_naoVerificado);
    }

    /// <summary>
    /// Garante a ordem dos códigos: 401 sem token, 403 com token de entregador (antes do 404), 404 com
    /// qrToken inexistente, 403 não verificado e 200; e que só os dois últimos gravam auditoria.
    /// </summary>
    /// <param name="caso">Situação.</param>
    /// <param name="esperado">Status esperado.</param>
    /// <param name="auditorias">Quantos AcessoCofre devem ser gravados.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("anonimo", 401, 0)]
    [DataRow("entregadorQrInexistente", 403, 0)]
    [DataRow("entregador", 403, 0)]
    [DataRow("qrInexistente", 404, 0)]
    [DataRow("naoVerificado", 403, 1)]
    [DataRow("verificado", 200, 1)]
    public async Task AcessarAsync_OrdemDosCodigos(string caso, int esperado, int auditorias)
    {
        // Arrange
        FakeUsuarioAtual usuario = caso switch
        {
            "anonimo" => FakeUsuarioAtual.Anonimo(),
            "entregador" or "entregadorQrInexistente" => FakeUsuarioAtual.Entregador(_entregador.Id),
            "naoVerificado" => FakeUsuarioAtual.Socorrista(_naoVerificado.Id),
            _ => FakeUsuarioAtual.Socorrista(_verificado.Id),
        };
        string qr = caso is "qrInexistente" or "entregadorQrInexistente" ? "qr-que-nao-existe" : _capacete.QrToken;

        // Act
        int status = await StatusAsync(() => Servico(usuario).AcessarAsync(qr, Ip, CancellationToken.None));

        // Assert
        Assert.AreEqual(esperado, status);
        Assert.HasCount(auditorias, _acessos.Acessos);
    }

    /// <summary>
    /// Garante a resposta do 200 com SOS ativo: origem, ocorrência, prazo, só os três campos de saúde
    /// (sem medicamentos, nome, CPF ou CNH) e o telefone completo do contato; e a auditoria concedida com o ip.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task AcessarAsync_ConcedidoComSosAtivo_RespostaMinimaEAuditoria()
    {
        // Arrange
        Ocorrencia ativa = SemearOcorrenciaAtiva();

        // Act
        AcessoCofreResposta resposta = await Servico(FakeUsuarioAtual.Socorrista(_verificado.Id)).AcessarAsync(_capacete.QrToken, Ip, CancellationToken.None);

        // Assert
        DateTime agora = _relogio.GetUtcNow().UtcDateTime;
        Assert.AreEqual(OrigemAcessoCofre.SosAtivo, resposta.Origem);
        Assert.AreEqual(ativa.Id, resposta.OcorrenciaId);
        Assert.AreEqual(agora, resposta.AcessadoEm);
        Assert.AreEqual(agora.AddMinutes(60), resposta.ExpiraEm);
        Assert.AreEqual("O+", resposta.TipoSanguineo);
        Assert.AreEqual("Dipirona", resposta.Alergias);
        Assert.AreEqual("Asma", resposta.CondicoesCriticas);
        Assert.AreEqual("11912345678", resposta.Contatos.Single().Telefone);

        string json = JsonSerializer.Serialize(resposta, JsonSerializerOptions.Web);
        foreach (string proibido in new[] { "Salbutamol", "medicamentos", _entregador.Nome, _entregador.Cpf, _entregador.Cnh, _capacete.QrToken })
        {
            Assert.DoesNotContain(proibido, json);
        }

        AcessoCofre auditoria = _acessos.Acessos.Single();
        Assert.IsTrue(auditoria.Concedido);
        Assert.IsNull(auditoria.Motivo);
        Assert.AreEqual(ativa.Id, auditoria.OcorrenciaId);
        Assert.AreEqual(Ip, auditoria.Ip);
        Assert.AreEqual(1, _unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante que o instante do acesso e o expiraEm saem com no máximo microssegundos, a precisão do
    /// PostgreSQL: o primeiro 200 (da memória) e os seguintes (relidos do banco) trazem o mesmo texto.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task AcessarAsync_RelogioCom100Nanossegundos_CortaEmMicrossegundos()
    {
        // Arrange: um instante com o último dígito de 100 ns diferente de zero.
        _relogio.SetUtcNow(new DateTimeOffset(new DateTime(2026, 11, 17, 23, 30, 28, DateTimeKind.Utc).AddTicks(6_062_469)));

        // Act
        AcessoCofreResposta resposta = await Servico(FakeUsuarioAtual.Socorrista(_verificado.Id)).AcessarAsync(_capacete.QrToken, Ip, CancellationToken.None);

        // Assert
        Assert.AreEqual(0, resposta.AcessadoEm.Ticks % 10);
        Assert.AreEqual(0, resposta.ExpiraEm.Ticks % 10);
        Assert.AreEqual(new DateTime(2026, 11, 17, 23, 30, 28, DateTimeKind.Utc).AddTicks(6_062_460), resposta.AcessadoEm);
    }

    /// <summary>
    /// Garante a emergência manual automática sem ocorrência ativa e o entregador sem ficha: 200 com os
    /// três campos de saúde nulos e os contatos.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task AcessarAsync_SemOcorrenciaESemFicha_EmergenciaManualComSaudeNula()
    {
        // Arrange
        var semFicha = new Entregador("Outro Simulado", "52998224725", "22222222222", "11922222222", CenarioSos.Queda);
        semFicha.AdicionarContato("Contato Simulado", "1131234567", "Pai");
        _entregadores.Semear(semFicha);
        var capacete = new Dispositivo("HC-0002", semFicha.Id);
        _dispositivos.Semear(capacete);

        // Act
        AcessoCofreResposta resposta = await Servico(FakeUsuarioAtual.Socorrista(_verificado.Id)).AcessarAsync(capacete.QrToken, Ip, CancellationToken.None);

        // Assert
        Assert.AreEqual(OrigemAcessoCofre.EmergenciaManual, resposta.Origem);
        Assert.IsNull(resposta.OcorrenciaId);
        Assert.IsNull(resposta.TipoSanguineo);
        Assert.IsNull(resposta.Alergias);
        Assert.IsNull(resposta.CondicoesCriticas);
        Assert.AreEqual("1131234567", resposta.Contatos.Single().Telefone);
    }

    /// <summary>
    /// Garante a auditoria do não verificado: negado, motivo, a origem e a ocorrência ativa, e o
    /// expiraEm igual ao acessadoEm; e o 403 com o motivo.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task AcessarAsync_NaoVerificado_AuditaANegacao()
    {
        // Arrange
        Ocorrencia ativa = SemearOcorrenciaAtiva();

        // Act
        AcessoCofreNegadoException excecao = await Assert.ThrowsExactlyAsync<AcessoCofreNegadoException>(
            () => Servico(FakeUsuarioAtual.Socorrista(_naoVerificado.Id)).AcessarAsync(_capacete.QrToken, Ip, CancellationToken.None));

        // Assert
        AcessoCofre auditoria = _acessos.Acessos.Single();
        Assert.AreEqual(MotivoNegacaoCofre.SocorristaNaoVerificado, excecao.Motivo);
        Assert.IsFalse(auditoria.Concedido);
        Assert.AreEqual(MotivoNegacaoCofre.SocorristaNaoVerificado, auditoria.Motivo);
        Assert.AreEqual(OrigemAcessoCofre.SosAtivo, auditoria.Origem);
        Assert.AreEqual(ativa.Id, auditoria.OcorrenciaId);
        Assert.AreEqual(auditoria.AcessadoEm, auditoria.ExpiraEm);
    }

    /// <summary>
    /// Garante o fluxo da janela no serviço: concedido com SOS ativo; o fim da ocorrência fecha a janela
    /// (403 ACESSO_EXPIRADO, auditado com o expiraEm do fechamento e origem EMERGENCIA_MANUAL); depois do
    /// bloqueio, abre a emergência manual.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task AcessarAsync_FimDaOcorrencia_ExpiraEDepoisDoBloqueioAbreManual()
    {
        // Arrange
        Ocorrencia ativa = SemearOcorrenciaAtiva();
        CofreServico servico = Servico(FakeUsuarioAtual.Socorrista(_verificado.Id));
        await servico.AcessarAsync(_capacete.QrToken, Ip, CancellationToken.None);
        _relogio.Advance(TimeSpan.FromMinutes(5));
        DateTime fim = _relogio.GetUtcNow().UtcDateTime;
        ativa.Encerrar(fim);
        _relogio.Advance(TimeSpan.FromMinutes(1));

        // Act
        AcessoCofreNegadoException excecao = await Assert.ThrowsExactlyAsync<AcessoCofreNegadoException>(
            () => servico.AcessarAsync(_capacete.QrToken, Ip, CancellationToken.None));
        _relogio.Advance(TimeSpan.FromMinutes(60));
        AcessoCofreResposta depois = await servico.AcessarAsync(_capacete.QrToken, Ip, CancellationToken.None);

        // Assert
        AcessoCofre negado = _acessos.Acessos[1];
        Assert.AreEqual(MotivoNegacaoCofre.AcessoExpirado, excecao.Motivo);
        Assert.IsFalse(negado.Concedido);
        Assert.AreEqual(fim, negado.ExpiraEm);
        Assert.AreEqual(OrigemAcessoCofre.EmergenciaManual, negado.Origem);
        Assert.IsNull(negado.OcorrenciaId);
        Assert.AreEqual(OrigemAcessoCofre.EmergenciaManual, depois.Origem);
        Assert.HasCount(3, _acessos.Acessos);
    }

    /// <summary>
    /// Garante que o log das tentativas leva só ids e o resultado, nunca o qrToken; e que o 404 e o token
    /// de entregador ficam só no log, com o id do usuário.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task AcessarAsync_Log_SemQrTokenComIdsEResultado()
    {
        // Act
        await Servico(FakeUsuarioAtual.Socorrista(_verificado.Id)).AcessarAsync(_capacete.QrToken, Ip, CancellationToken.None);
        await StatusAsync(() => Servico(FakeUsuarioAtual.Socorrista(_verificado.Id)).AcessarAsync("qr-inexistente-secreto", Ip, CancellationToken.None));
        await StatusAsync(() => Servico(FakeUsuarioAtual.Entregador(_entregador.Id)).AcessarAsync(_capacete.QrToken, Ip, CancellationToken.None));

        // Assert
        string[] mensagens = _logger.Registros.Select(r => r.Mensagem).ToArray();
        Assert.HasCount(3, mensagens);
        Assert.Contains($"socorrista {_verificado.Id}, entregador {_entregador.Id}, resultado CONCEDIDO", mensagens[0]);
        Assert.Contains($"QR Code inexistente, socorrista {_verificado.Id}", mensagens[1]);
        Assert.Contains($"token de entregador {_entregador.Id}", mensagens[2]);
        Assert.IsFalse(mensagens.Any(m => m.Contains(_capacete.QrToken, StringComparison.Ordinal) || m.Contains("secreto", StringComparison.Ordinal) || m.Contains(Ip, StringComparison.Ordinal)));
    }

    /// <summary>
    /// Garante a auditoria do entregador: só o dono (outro entregador e socorrista recebem 403, entregador
    /// inexistente 404), em ordem decrescente de acessadoEm, com o socorrista e sem ip nem saúde.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ListarAuditoriaAsync_DoDono_OrdemDecrescenteSemIp()
    {
        // Arrange: uma negação (não verificado) e, 5 min depois, uma concessão.
        await StatusAsync(() => Servico(FakeUsuarioAtual.Socorrista(_naoVerificado.Id)).AcessarAsync(_capacete.QrToken, Ip, CancellationToken.None));
        _relogio.Advance(TimeSpan.FromMinutes(5));
        await Servico(FakeUsuarioAtual.Socorrista(_verificado.Id)).AcessarAsync(_capacete.QrToken, Ip, CancellationToken.None);

        // Act
        IReadOnlyList<ItemAuditoriaCofreResposta> itens = await Servico(FakeUsuarioAtual.Entregador(_entregador.Id))
            .ListarAuditoriaAsync(_entregador.Id, CancellationToken.None);

        // Assert
        Assert.HasCount(2, itens);
        Assert.IsTrue(itens[0].AcessadoEm > itens[1].AcessadoEm);
        Assert.IsTrue(itens[0].Concedido);
        Assert.AreEqual("Socorrista Simulado", itens[0].Socorrista.Nome);
        Assert.AreEqual(MotivoNegacaoCofre.SocorristaNaoVerificado, itens[1].Motivo);
        string json = JsonSerializer.Serialize(itens, JsonSerializerOptions.Web);
        Assert.DoesNotContain(Ip, json);
        Assert.DoesNotContain("tipoSanguineo", json);

        Assert.AreEqual(403, await StatusAsync(() => Servico(FakeUsuarioAtual.Entregador(Guid.NewGuid())).ListarAuditoriaAsync(_entregador.Id, CancellationToken.None)));
        Assert.AreEqual(403, await StatusAsync(() => Servico(FakeUsuarioAtual.Socorrista(_verificado.Id)).ListarAuditoriaAsync(_entregador.Id, CancellationToken.None)));
        Assert.AreEqual(404, await StatusAsync(() => Servico(FakeUsuarioAtual.Entregador(_entregador.Id)).ListarAuditoriaAsync(Guid.NewGuid(), CancellationToken.None)));
    }

    private static async Task<int> StatusAsync(Func<Task> acao)
    {
        try
        {
            await acao();
            return 200;
        }
        catch (NaoAutenticadoException)
        {
            return 401;
        }
        catch (AcessoNegadoException)
        {
            return 403;
        }
        catch (AcessoCofreNegadoException)
        {
            return 403;
        }
        catch (RecursoNaoEncontradoException)
        {
            return 404;
        }
    }

    private Ocorrencia SemearOcorrenciaAtiva()
    {
        Ocorrencia ocorrencia = CenarioSos.AbrirOcorrencia(_entregador.Id);
        _ocorrencias.Semear(ocorrencia);
        return ocorrencia;
    }

    private CofreServico Servico(FakeUsuarioAtual usuario)
    {
        return new CofreServico(
            new AutorizacaoRecursos(usuario, _voluntarios),
            _dispositivos,
            _socorristas,
            _entregadores,
            _ocorrencias,
            _acessos,
            _unidade,
            _relogio,
            Options.Create(new OpcoesHelmCare()),
            _logger);
    }
}
