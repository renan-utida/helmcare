using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Api.Seguranca;

/// <summary>
/// Regras de "quem pode o quê" da seção 7 do contrato (1.15), num lugar só. Os serviços chamam estes
/// métodos depois de carregar o recurso, o que dá a ordem do contrato: <c>401</c> (sem token), depois
/// <c>404</c> (o serviço não achou o recurso) e por último <c>403</c> (outro dono ou papel errado).
/// </summary>
public interface IAutorizacaoRecursos
{
    /// <summary>
    /// Id do entregador do token, ou nulo quando o token não é de um entregador. Não lança: serve para
    /// decidir o que mostrar (por exemplo, a <c>distanciaVoluntarioKm</c> só para a vítima).
    /// </summary>
    Guid? EntregadorDoToken { get; }

    /// <summary>
    /// Exige um token de entregador.
    /// </summary>
    /// <returns>O id do entregador do token.</returns>
    /// <exception cref="Servicos.Comum.NaoAutenticadoException">Sem token (401).</exception>
    /// <exception cref="Servicos.Comum.AcessoNegadoException">Token de outro papel (403).</exception>
    Guid ExigirEntregador();

    /// <summary>
    /// Exige que o recurso seja do entregador do token.
    /// </summary>
    /// <param name="entregadorId">Dono do recurso (ou o <c>entregadorId</c> do corpo).</param>
    /// <exception cref="Servicos.Comum.NaoAutenticadoException">Sem token (401).</exception>
    /// <exception cref="Servicos.Comum.AcessoNegadoException">Outro dono ou papel errado (403).</exception>
    void ExigirDono(Guid entregadorId);

    /// <summary>
    /// Exige um token de socorrista (rotas do Cofre).
    /// </summary>
    /// <returns>O id do socorrista do token.</returns>
    /// <exception cref="Servicos.Comum.NaoAutenticadoException">Sem token (401).</exception>
    /// <exception cref="Servicos.Comum.AcessoNegadoException">Token de outro papel, como o de entregador (403).</exception>
    Guid ExigirSocorrista();

    /// <summary>
    /// <c>GET /ocorrencias/{id}</c>: a vítima ou qualquer entregador que seja voluntário. O voluntário
    /// informado na consulta, se houver, precisa ser o do token.
    /// </summary>
    /// <param name="ocorrencia">Ocorrência consultada.</param>
    /// <param name="voluntarioInformado">Voluntário do parâmetro <c>voluntarioId</c>, já encontrado, ou nulo.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>A tarefa da conferência.</returns>
    Task ExigirLeituraOcorrenciaAsync(Ocorrencia ocorrencia, Voluntario? voluntarioInformado, CancellationToken cancellationToken);

    /// <summary>
    /// <c>POST /ocorrencias/{id}/encerrar</c>: a vítima ou o voluntário que aceitou.
    /// </summary>
    /// <param name="ocorrencia">Ocorrência, com os aceites carregados.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>A tarefa da conferência.</returns>
    Task ExigirEncerramentoAsync(Ocorrencia ocorrencia, CancellationToken cancellationToken);
}
