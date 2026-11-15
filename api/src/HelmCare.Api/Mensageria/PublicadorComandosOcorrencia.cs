using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using Microsoft.Extensions.Logging;

namespace HelmCare.Api.Mensageria;

/// <summary>
/// Único ponto da regra "publicar sem bloquear" dos comandos da ocorrência (<c>SOCORRO_A_CAMINHO</c>
/// e <c>ENCERRAR</c>), usado pelo aceite, pelo cancelar, pelo encerrar e pelo <c>SOS</c> repetido
/// (seção 7, "Regras do SOS na API").
/// </summary>
/// <remarks>
/// O comando é publicado depois de gravar, e nenhuma falha (broker fora, capacete não encontrado,
/// banco) sai daqui: vira aviso no log e a operação segue, porque o socorro não pode depender do
/// capacete. O comando perdido é refeito no próximo reenvio do <c>SOS</c> (seção 6.2). Os comandos
/// do turno não passam por aqui e continuam respondendo 503.
/// </remarks>
public sealed partial class PublicadorComandosOcorrencia
{
    private readonly IPublicadorComandos _publicador;
    private readonly IDispositivoRepositorio _dispositivos;
    private readonly ILogger<PublicadorComandosOcorrencia> _logger;

    /// <summary>
    /// Cria o publicador.
    /// </summary>
    /// <param name="publicador">Publicador de comandos no broker.</param>
    /// <param name="dispositivos">Repositório de capacetes (para achar o capacete da vítima).</param>
    /// <param name="logger">Log da aplicação.</param>
    public PublicadorComandosOcorrencia(
        IPublicadorComandos publicador,
        IDispositivoRepositorio dispositivos,
        ILogger<PublicadorComandosOcorrencia> logger)
    {
        _publicador = publicador;
        _dispositivos = dispositivos;
        _logger = logger;
    }

    /// <summary>
    /// Publica o comando no capacete do entregador da ocorrência, sem lançar exceção.
    /// </summary>
    /// <param name="ocorrencia">Ocorrência já gravada.</param>
    /// <param name="tipo">Comando (<c>SOCORRO_A_CAMINHO</c> ou <c>ENCERRAR</c>).</param>
    /// <param name="cancellationToken">Cancelamento da operação.</param>
    /// <returns><see langword="true"/> se o comando chegou ao broker.</returns>
    public async Task<bool> PublicarSemBloquearAsync(Ocorrencia ocorrencia, TipoComandoMqtt tipo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ocorrencia);

        string comando = NomeContrato.Para(tipo);

        try
        {
            Dispositivo? capacete = await _dispositivos.ObterDoEntregadorAsync(ocorrencia.EntregadorId, cancellationToken);

            if (capacete is null)
            {
                RegistrarSemCapacete(_logger, comando, ocorrencia.Id);
                return false;
            }

            return await PublicarSemBloquearAsync(capacete.Codigo, ocorrencia.Id, tipo, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            RegistrarFalha(_logger, comando, ocorrencia.Id, "?", ex.GetType().Name);
            return false;
        }
    }

    /// <summary>
    /// Publica o comando no capacete informado, sem lançar exceção (usado quando o código já é conhecido,
    /// como no <c>SOS</c> repetido).
    /// </summary>
    /// <param name="codigoDispositivo">Código do capacete.</param>
    /// <param name="ocorrenciaId">Ocorrência a que o comando se refere (só para o log).</param>
    /// <param name="tipo">Comando (<c>SOCORRO_A_CAMINHO</c> ou <c>ENCERRAR</c>).</param>
    /// <param name="cancellationToken">Cancelamento da operação.</param>
    /// <returns><see langword="true"/> se o comando chegou ao broker.</returns>
    public async Task<bool> PublicarSemBloquearAsync(
        string codigoDispositivo,
        Guid ocorrenciaId,
        TipoComandoMqtt tipo,
        CancellationToken cancellationToken)
    {
        try
        {
            await _publicador.PublicarAsync(codigoDispositivo, tipo, cancellationToken);
            return true;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // MensageriaIndisponivelException (broker fora) ou qualquer outra falha da publicação.
            RegistrarFalha(_logger, NomeContrato.Para(tipo), ocorrenciaId, codigoDispositivo, ex.GetType().Name);
            return false;
        }
    }

    [LoggerMessage(EventId = 410, Level = LogLevel.Warning, Message = "Comando {Comando} da ocorrência {OcorrenciaId} não publicado no capacete {Codigo} ({TipoExcecao}); a operação segue e o comando é refeito no próximo reenvio do SOS.")]
    private static partial void RegistrarFalha(ILogger logger, string comando, Guid ocorrenciaId, string codigo, string tipoExcecao);

    [LoggerMessage(EventId = 411, Level = LogLevel.Warning, Message = "Comando {Comando} da ocorrência {OcorrenciaId} não publicado: o entregador não tem capacete vinculado.")]
    private static partial void RegistrarSemCapacete(ILogger logger, string comando, Guid ocorrenciaId);
}
