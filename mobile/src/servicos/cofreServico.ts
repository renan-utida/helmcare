import type { ItemAuditoriaCofreResposta } from '@/tipos/contrato';

import { api } from './api';

/** Rotas do Cofre médico que o app do entregador usa (seção 7). */
export const cofreServico = {
  /**
   * `GET /cofre/auditoria/{entregadorId}`: toda tentativa de um socorrista de abrir a ficha do
   * entregador, concedida ou negada, com o socorrista (nome e organização). Só para o próprio
   * entregador. Nunca traz dados de saúde nem o ip. Fica só na memória da tela, nunca no SQLite.
   */
  async listarAuditoria(entregadorId: string): Promise<ItemAuditoriaCofreResposta[]> {
    const { data } = await api.get<ItemAuditoriaCofreResposta[]>(
      `/cofre/auditoria/${encodeURIComponent(entregadorId)}`,
    );
    return data;
  },
};
