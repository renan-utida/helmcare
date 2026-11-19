import type { SQLiteDatabase } from 'expo-sqlite';

/** Chaves usadas na tabela `preferencia`. */
export const CHAVES_PREFERENCIA = {
  /** URL da API salva à mão na aba Ajustes. */
  urlApiManual: 'url_api_manual',
  /** Modo de cor escolhido nos Ajustes: "claro" ou "escuro" ("sistema" é da versão antiga e é migrado). */
  tema: 'tema',
  /** Paleta do modo claro: "helmcare", "alto-contraste" ou "areia". */
  paletaClara: 'paleta_claro',
  /** Paleta do modo escuro: "helmcare", "alto-contraste" ou "grafite". */
  paletaEscura: 'paleta_escuro',
} as const;

/** Uma das chaves aceitas pela tabela `preferencia`. */
export type ChavePreferencia = (typeof CHAVES_PREFERENCIA)[keyof typeof CHAVES_PREFERENCIA];

/** Repositório da tabela `preferencia` (chave e valor). Todas as consultas usam `?`. */
export const preferenciaRepositorio = {
  /** Lê o valor de uma chave, ou `null` se ela não existir. */
  async obter(db: SQLiteDatabase, chave: ChavePreferencia): Promise<string | null> {
    const linha = await db.getFirstAsync<{ valor: string }>(
      'SELECT valor FROM preferencia WHERE chave = ?',
      chave,
    );
    return linha?.valor ?? null;
  },

  /** Grava ou troca o valor de uma chave. */
  async definir(db: SQLiteDatabase, chave: ChavePreferencia, valor: string): Promise<void> {
    await db.runAsync(
      `INSERT INTO preferencia (chave, valor) VALUES (?, ?)
       ON CONFLICT (chave) DO UPDATE SET valor = excluded.valor`,
      chave,
      valor,
    );
  },

  /** Apaga a chave (por exemplo, para voltar à URL automática). */
  async remover(db: SQLiteDatabase, chave: ChavePreferencia): Promise<void> {
    await db.runAsync('DELETE FROM preferencia WHERE chave = ?', chave);
  },
};
