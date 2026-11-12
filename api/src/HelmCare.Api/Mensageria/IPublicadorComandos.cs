using System.Threading;
using System.Threading.Tasks;
using HelmCare.Dominio.Enums;

namespace HelmCare.Api.Mensageria;

/// <summary>
/// Envia comandos ao capacete (seção 6.3). Os serviços dependem desta interface; os
/// controllers da rodada 3 (turnos e ocorrências) são os primeiros a usá-la.
/// </summary>
public interface IPublicadorComandos
{
    /// <summary>
    /// Publica o comando em <c>{raiz}/{codigo}/comando</c>, em QoS 1 e sem retain.
    /// </summary>
    /// <param name="codigoDispositivo">Código do capacete (ex.: <c>HC-0001</c>).</param>
    /// <param name="tipo">Comando.</param>
    /// <param name="cancellationToken">Cancelamento da operação.</param>
    /// <returns>A tarefa da publicação.</returns>
    /// <exception cref="Servicos.Comum.MensageriaIndisponivelException">Quando a API não está conectada ao broker (503).</exception>
    Task PublicarAsync(string codigoDispositivo, TipoComandoMqtt tipo, CancellationToken cancellationToken);
}
