import type {
  AlertaTurnoResposta,
  IniciarTurnoRequisicao,
  RiscoTurnoResposta,
  TurnoResposta,
} from '@/tipos/contrato';

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

  /**
   * `GET /turnos/{id}/alerta`: estado do capacete no turno. Sem capacete vinculado, 200 com
   * todos os campos `null`. O `contagemTerminaEm` está no relógio do capacete: só informação.
   */
  async obterAlerta(turnoId: string): Promise<AlertaTurnoResposta> {
    const { data } = await api.get<AlertaTurnoResposta>(`/turnos/${encodeURIComponent(turnoId)}/alerta`);
    return data;
  },

  /**
   * `POST /turnos/{id}/alerta/cancelar`: 202 em CONTAGEM (a API publicou o `CANCELAR`);
   * 409 em qualquer outro estado (por exemplo, o SOS já saiu); 503 o comando não chegou ao broker.
   */
  async cancelarAlerta(turnoId: string): Promise<void> {
    await api.post(`/turnos/${encodeURIComponent(turnoId)}/alerta/cancelar`);
  },
};
