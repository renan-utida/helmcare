import type { SQLiteDatabase } from 'expo-sqlite';

import type { EstadoDispositivo } from '@/tipos/contrato';

/** Uma mudança de estado do capacete vista pelo app, para o histórico offline. */
export interface AlertaLocal {
  id: number;
  turnoId: string;
  estado: EstadoDispositivo;
  registradoEm: string;
}

/** Linha da tabela `alerta`, com os nomes das colunas. */
interface LinhaAlerta {
  id: number;
  turno_id: string;
  estado: EstadoDispositivo;
  registrado_em: string;
}

/** Converte a linha do SQLite no objeto usado pelo app. */
function paraAlerta(linha: LinhaAlerta): AlertaLocal {
  return { id: linha.id, turnoId: linha.turno_id, estado: linha.estado, registradoEm: linha.registrado_em };
}

/** Repositório da tabela `alerta`. Todas as consultas usam parâmetros `?`. */
export const alertaRepositorio = {
  /**
   * Registra o estado do capacete visto numa consulta, sem repetir o mesmo estado seguido
   * do mesmo turno (o polling lê o mesmo estado várias vezes). Devolve se gravou.
   */
  async registrar(db: SQLiteDatabase, turnoId: string, estado: EstadoDispositivo): Promise<boolean> {
    const ultimo = await db.getFirstAsync<{ estado: string }>(
      'SELECT estado FROM alerta WHERE turno_id = ? ORDER BY id DESC LIMIT 1',
      turnoId,
    );
    if (ultimo?.estado === estado) {
      return false;
    }
    await db.runAsync(
      'INSERT INTO alerta (turno_id, estado, registrado_em) VALUES (?, ?, ?)',
      turnoId,
      estado,
      new Date().toISOString(),
    );
    return true;
  },

  /** Mudanças de estado dos turnos do entregador, da mais recente para a mais antiga. */
  async listar(db: SQLiteDatabase, entregadorId: string): Promise<AlertaLocal[]> {
    const linhas = await db.getAllAsync<LinhaAlerta>(
      `SELECT alerta.* FROM alerta
       JOIN turno ON turno.id = alerta.turno_id
       WHERE turno.entregador_id = ?
       ORDER BY alerta.id DESC`,
      entregadorId,
    );
    return linhas.map(paraAlerta);
  },
};
