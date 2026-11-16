import type { TrechosCriticosResposta } from '@/tipos/contrato';

import { api } from './api';

/** Rota dos trechos críticos, que a API repassa do Serviço de Risco (seção 7). */
export const trechoServico = {
  /**
   * `GET /trechos-criticos?limite=`: lista em ordem decrescente de sinistros graves.
   * `limite` de 1 a 50 (fora disso, 400). Serviço de Risco fora: 503, sem lista simulada.
   */
  async listar(limite: number): Promise<TrechosCriticosResposta> {
    const { data } = await api.get<TrechosCriticosResposta>('/trechos-criticos', { params: { limite } });
    return data;
  },
};
