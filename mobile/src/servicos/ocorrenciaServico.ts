import type { OcorrenciaResposta } from '@/tipos/contrato';

import { api } from './api';

/** Rotas da ocorrência (o SOS) usadas pela vítima nesta entrega (seção 7). */
export const ocorrenciaServico = {
  /**
   * `GET /ocorrencias/{id}?voluntarioId=`: estado, camada e cronômetro. Sem `voluntarioId`
   * (caso da vítima), a localização vem aproximada. 404 ocorrência inexistente.
   */
  async obter(id: string, voluntarioId?: string): Promise<OcorrenciaResposta> {
    const { data } = await api.get<OcorrenciaResposta>(`/ocorrencias/${encodeURIComponent(id)}`, {
      params: voluntarioId ? { voluntarioId } : undefined,
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
   * `POST /ocorrencias/{id}/encerrar`: encerra com ou sem aceite. 200 com a ocorrência
   * ENCERRADA; 409 já encerrada ou conflito com o escalonamento (o app repete a chamada).
   */
  async encerrar(id: string): Promise<OcorrenciaResposta> {
    const { data } = await api.post<OcorrenciaResposta>(`/ocorrencias/${encodeURIComponent(id)}/encerrar`);
    return data;
  },
};
