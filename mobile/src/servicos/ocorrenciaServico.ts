import type { OcorrenciaProximaResposta, OcorrenciaResposta } from '@/tipos/contrato';

import { api } from './api';

/** Rotas da ocorrência (o SOS), usadas pela vítima e pelo voluntário (seção 7). */
export const ocorrenciaServico = {
  /**
   * `GET /ocorrencias/{id}?voluntarioId=`: estado, camada e cronômetro. A vítima consulta sem
   * `voluntarioId` (localização aproximada); o voluntário manda o próprio id, e a localização só
   * vem exata (`localizacaoExata` verdadeiro) se foi ele quem aceitou. 404 ocorrência inexistente.
   */
  async obter(id: string, voluntarioId?: string): Promise<OcorrenciaResposta> {
    const { data } = await api.get<OcorrenciaResposta>(`/ocorrencias/${encodeURIComponent(id)}`, {
      params: voluntarioId ? { voluntarioId } : undefined,
    });
    return data;
  },

  /**
   * `GET /ocorrencias/proximas?voluntarioId=`: chamados que o voluntário pode atender, pela
   * disponibilidade e pela posição do último `PUT` (camada 1: até 2 km; camada 2: todos). Sempre
   * com a localização aproximada, em ordem crescente de distância. Indisponível recebe a lista
   * vazia. 400 sem `voluntarioId`; 404 voluntário inexistente.
   */
  async listarProximas(voluntarioId: string): Promise<OcorrenciaProximaResposta[]> {
    const { data } = await api.get<OcorrenciaProximaResposta[]>('/ocorrencias/proximas', {
      params: { voluntarioId },
    });
    return data;
  },

  /**
   * `POST /ocorrencias/{id}/aceites`: o voluntário aceita o chamado. 201 com o corpo do
   * `GET /ocorrencias/{id}` e a localização exata para ele; 409 já aceito por outro ou ele não
   * pode atender (indisponível, é a vítima ou está fora do raio na camada 1); 404 inexistente.
   */
  async aceitar(id: string, voluntarioId: string): Promise<OcorrenciaResposta> {
    const { data } = await api.post<OcorrenciaResposta>(`/ocorrencias/${encodeURIComponent(id)}/aceites`, {
      voluntarioId,
    });
    return data;
  },

  /**
   * `POST /ocorrencias/{id}/cancelar`: a vítima informa que está bem. 200 com a ocorrência
   * CANCELADA; 409 já encerrada ou conflito com o escalonamento (o app repete a chamada).
   */
  async cancelar(id: string): Promise<OcorrenciaResposta> {
    const { data } = await api.post<OcorrenciaResposta>(`/ocorrencias/${encodeURIComponent(id)}/cancelar`);
    return data;
  },

  /**
   * `POST /ocorrencias/{id}/encerrar`: encerra com ou sem aceite (vítima ou voluntário). 200 com
   * a ocorrência ENCERRADA; 409 já encerrada ou conflito com o escalonamento (o app repete).
   */
  async encerrar(id: string): Promise<OcorrenciaResposta> {
    const { data } = await api.post<OcorrenciaResposta>(`/ocorrencias/${encodeURIComponent(id)}/encerrar`);
    return data;
  },
};
