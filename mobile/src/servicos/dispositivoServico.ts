import type { DispositivoResposta, VincularDispositivoRequisicao } from '@/tipos/contrato';

import { api } from './api';

/** Rotas do capacete (seção 7). */
export const dispositivoServico = {
  /**
   * `POST /dispositivos`: 201 com o capacete vinculado; 409 capacete já vinculado; 400; 404.
   * A resposta traz `qrToken` e `urlCofre`, que o app não guarda em lugar nenhum.
   */
  async vincular(dados: VincularDispositivoRequisicao): Promise<DispositivoResposta> {
    const { data } = await api.post<DispositivoResposta>('/dispositivos', dados);
    return data;
  },
};
