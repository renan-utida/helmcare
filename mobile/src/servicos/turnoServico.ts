import type { IniciarTurnoRequisicao, RiscoTurnoResposta, TurnoResposta } from '@/tipos/contrato';

import { api } from './api';

/** Rotas do turno (seção 7). */
export const turnoServico = {
  /**
   * `POST /turnos`: 201 com o Índice de Risco. 409 turno ativo ou proteção sem capacete;
   * 503 o comando `ARMAR` não chegou ao capacete (nada foi gravado); 400 duração inválida.
   */
  async iniciar(dados: IniciarTurnoRequisicao): Promise<RiscoTurnoResposta> {
    const { data } = await api.post<RiscoTurnoResposta>('/turnos', dados);
    return data;
  },

  /** `GET /turnos/{id}/risco`: 200 com o Índice guardado no início do turno; 404. */
  async obterRisco(turnoId: string): Promise<RiscoTurnoResposta> {
    const { data } = await api.get<RiscoTurnoResposta>(`/turnos/${encodeURIComponent(turnoId)}/risco`);
    return data;
  },

  /**
   * `POST /turnos/{id}/encerrar`: 200 com o turno e o `fim`. 409 alerta ou ocorrência em
   * andamento; 503 o `DESARMAR` não chegou ao capacete; 404 turno inexistente.
   */
  async encerrar(turnoId: string): Promise<TurnoResposta> {
    const { data } = await api.post<TurnoResposta>(`/turnos/${encodeURIComponent(turnoId)}/encerrar`);
    return data;
  },
};
