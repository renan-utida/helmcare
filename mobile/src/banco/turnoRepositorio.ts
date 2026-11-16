import type { SQLiteDatabase } from 'expo-sqlite';

import type { FaixaRisco } from '@/tipos/contrato';

/**
 * Turno salvo no aparelho, com o último Índice de Risco recebido.
 * Serve para mostrar o turno e o Índice sem conexão e para o histórico offline.
 */
export interface TurnoLocal {
  id: string;
  entregadorId: string;
  inicio: string;
  fimPrevisto: string;
  fim: string | null;
  protecaoAtiva: boolean;
  indiceRisco: number;
  faixaRisco: FaixaRisco;
  componenteSinistro: number;
  componenteChuva: number;
  componenteCalor: number;
  explicacao: string;
  atualizadoEm: string;
}

/** Linha da tabela `turno`, com os nomes das colunas. */
interface LinhaTurno {
  id: string;
  entregador_id: string;
  inicio: string;
  fim_previsto: string;
  fim: string | null;
  protecao_ativa: number;
  indice_risco: number;
  faixa_risco: FaixaRisco;
  componente_sinistro: number;
  componente_chuva: number;
  componente_calor: number;
  explicacao: string;
  atualizado_em: string;
}

/** Converte a linha do SQLite (booleano como 0 ou 1) no objeto usado pelo app. */
function paraTurno(linha: LinhaTurno): TurnoLocal {
  return {
    id: linha.id,
    entregadorId: linha.entregador_id,
    inicio: linha.inicio,
    fimPrevisto: linha.fim_previsto,
    fim: linha.fim,
    protecaoAtiva: linha.protecao_ativa === 1,
    indiceRisco: linha.indice_risco,
    faixaRisco: linha.faixa_risco,
    componenteSinistro: linha.componente_sinistro,
    componenteChuva: linha.componente_chuva,
    componenteCalor: linha.componente_calor,
    explicacao: linha.explicacao,
    atualizadoEm: linha.atualizado_em,
  };
}

/** Repositório da tabela `turno`. Todas as consultas usam parâmetros `?`. */
export const turnoRepositorio = {
  /** Grava o turno ou atualiza o que já existe com o mesmo id (por exemplo, o Índice novo). */
  async salvar(db: SQLiteDatabase, turno: TurnoLocal): Promise<void> {
    await db.runAsync(
      `INSERT INTO turno (id, entregador_id, inicio, fim_previsto, fim, protecao_ativa, indice_risco,
         faixa_risco, componente_sinistro, componente_chuva, componente_calor, explicacao, atualizado_em)
       VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
       ON CONFLICT (id) DO UPDATE SET
         fim = excluded.fim,
         indice_risco = excluded.indice_risco,
         faixa_risco = excluded.faixa_risco,
         componente_sinistro = excluded.componente_sinistro,
         componente_chuva = excluded.componente_chuva,
         componente_calor = excluded.componente_calor,
         explicacao = excluded.explicacao,
         atualizado_em = excluded.atualizado_em`,
      turno.id,
      turno.entregadorId,
      turno.inicio,
      turno.fimPrevisto,
      turno.fim,
      turno.protecaoAtiva ? 1 : 0,
      turno.indiceRisco,
      turno.faixaRisco,
      turno.componenteSinistro,
      turno.componenteChuva,
      turno.componenteCalor,
      turno.explicacao,
      turno.atualizadoEm,
    );
  },

  /** Turno ainda aberto (sem `fim`) do entregador, o mais recente, ou `null`. */
  async obterAtivo(db: SQLiteDatabase, entregadorId: string): Promise<TurnoLocal | null> {
    const linha = await db.getFirstAsync<LinhaTurno>(
      'SELECT * FROM turno WHERE entregador_id = ? AND fim IS NULL ORDER BY inicio DESC LIMIT 1',
      entregadorId,
    );
    return linha ? paraTurno(linha) : null;
  },

  /** Marca o turno como encerrado no horário informado (ISO UTC). */
  async marcarEncerrado(db: SQLiteDatabase, id: string, fim: string): Promise<void> {
    await db.runAsync(
      'UPDATE turno SET fim = ?, atualizado_em = ? WHERE id = ?',
      fim,
      new Date().toISOString(),
      id,
    );
  },

  /** Turnos do entregador, do mais recente para o mais antigo (histórico). */
  async listarDoEntregador(db: SQLiteDatabase, entregadorId: string): Promise<TurnoLocal[]> {
    const linhas = await db.getAllAsync<LinhaTurno>(
      'SELECT * FROM turno WHERE entregador_id = ? ORDER BY inicio DESC',
      entregadorId,
    );
    return linhas.map(paraTurno);
  },
};
