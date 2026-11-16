import type { SQLiteDatabase } from 'expo-sqlite';

import type { TrechoCriticoResposta } from '@/tipos/contrato';

/** Lista de trechos críticos guardada no aparelho (dado público, pode ter coordenadas). */
export interface TrechosEmCache {
  periodo: string;
  /** Quando a lista foi salva (ISO UTC). */
  atualizadoEm: string;
  trechos: TrechoCriticoResposta[];
}

/** Linha da tabela `trecho_cache`, com os nomes das colunas. */
interface LinhaTrecho {
  posicao: number;
  logradouro: string;
  latitude: number;
  longitude: number;
  quantidade_sinistros_graves: number;
  periodo: string;
  atualizado_em: string;
}

/** Repositório da tabela `trecho_cache`. Todas as consultas usam parâmetros `?`. */
export const trechoRepositorio = {
  /** Troca a lista inteira do cache pela nova, numa transação (ou troca tudo, ou nada). */
  async substituir(db: SQLiteDatabase, periodo: string, trechos: TrechoCriticoResposta[]): Promise<void> {
    const agora = new Date().toISOString();
    await db.withTransactionAsync(async () => {
      await db.runAsync('DELETE FROM trecho_cache');
      for (const trecho of trechos) {
        await db.runAsync(
          `INSERT INTO trecho_cache (posicao, logradouro, latitude, longitude, quantidade_sinistros_graves,
             periodo, atualizado_em)
           VALUES (?, ?, ?, ?, ?, ?, ?)`,
          trecho.posicao,
          trecho.logradouro,
          trecho.latitude,
          trecho.longitude,
          trecho.quantidadeSinistrosGraves,
          periodo,
          agora,
        );
      }
    });
  },

  /** Lista salva, em ordem de posição, ou `null` quando o cache está vazio. */
  async listar(db: SQLiteDatabase): Promise<TrechosEmCache | null> {
    const linhas = await db.getAllAsync<LinhaTrecho>('SELECT * FROM trecho_cache ORDER BY posicao ASC');
    const primeira = linhas[0];
    if (!primeira) {
      return null;
    }
    return {
      periodo: primeira.periodo,
      atualizadoEm: primeira.atualizado_em,
      trechos: linhas.map((linha) => ({
        posicao: linha.posicao,
        logradouro: linha.logradouro,
        latitude: linha.latitude,
        longitude: linha.longitude,
        quantidadeSinistrosGraves: linha.quantidade_sinistros_graves,
      })),
    };
  },
};
