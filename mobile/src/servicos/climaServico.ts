import type { ClimaResposta } from '@/tipos/contrato';

import { api } from './api';

/** Rota do clima (contrato 1.17, seção 7, "Previsão do tempo"). */
export const climaServico = {
  /**
   * `GET /clima`: condições atuais da cidade pela Open-Meteo, para o cartão "Clima agora" da aba
   * Início. O local é a coordenada fixa da cidade: o app não envia posição nenhuma. 200 com o
   * clima; 503 sem a Open-Meteo (o cartão some); 403 com token de socorrista. Fica fora do polling.
   */
  async obter(): Promise<ClimaResposta> {
    const { data } = await api.get<ClimaResposta>('/clima');
    return data;
  },
};
