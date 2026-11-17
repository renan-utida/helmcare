import type { DisponibilidadeRequisicao, VoluntarioResposta } from '@/tipos/contrato';

import { api } from './api';

/**
 * Rotas da rede de voluntários (seção 7, "Regras da rede de voluntários"). Sem login no
 * protótipo, o voluntário é identificado pelo `voluntarioId`. A posição enviada aqui é sempre
 * uma das posições simuladas (`src/utils/posicoesSimuladas.ts`) e nunca vai para log.
 */
export const voluntarioServico = {
  /**
   * `POST /voluntarios`: torna o entregador voluntário da rede, sempre do tipo ENTREGADOR.
   * 201 com `disponivel` falso e a posição `null`; 400 campo inválido; 404 entregador
   * inexistente; 409 o entregador já é voluntário (no máximo um por entregador).
   */
  async cadastrar(entregadorId: string): Promise<VoluntarioResposta> {
    const { data } = await api.post<VoluntarioResposta>('/voluntarios', { entregadorId, tipo: 'ENTREGADOR' });
    return data;
  },

  /**
   * `PUT /voluntarios/{id}/disponibilidade`: liga (com a posição) ou desliga a disponibilidade.
   * Indisponível manda só `{ disponivel: false }`, e a API apaga a posição. 200 no mesmo formato
   * do cadastro; 400 disponível sem as duas coordenadas; 404 voluntário inexistente.
   */
  async atualizarDisponibilidade(
    voluntarioId: string,
    disponibilidade: DisponibilidadeRequisicao,
  ): Promise<VoluntarioResposta> {
    const { data } = await api.put<VoluntarioResposta>(
      `/voluntarios/${encodeURIComponent(voluntarioId)}/disponibilidade`,
      disponibilidade,
    );
    return data;
  },
};
