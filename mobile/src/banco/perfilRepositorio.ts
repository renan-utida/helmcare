import type { SQLiteDatabase } from 'expo-sqlite';

/**
 * Perfil local do aparelho. Guarda só ids, o código do capacete e um apelido neutro
 * ("Entregador com capacete" ou "Voluntário"), nunca nome, CPF, CNH ou telefone.
 */
export interface Perfil {
  entregadorId: string;
  apelido: string;
  codigoCapacete: string | null;
  voluntarioId: string | null;
  criadoEm: string;
}

/** Linha da tabela `perfil`, com os nomes das colunas. */
interface LinhaPerfil {
  entregador_id: string;
  apelido: string;
  codigo_capacete: string | null;
  voluntario_id: string | null;
  criado_em: string;
}

/** Converte a linha do SQLite no objeto usado pelo app. */
function paraPerfil(linha: LinhaPerfil): Perfil {
  return {
    entregadorId: linha.entregador_id,
    apelido: linha.apelido,
    codigoCapacete: linha.codigo_capacete,
    voluntarioId: linha.voluntario_id,
    criadoEm: linha.criado_em,
  };
}

/** Repositório da tabela `perfil`. Todas as consultas usam parâmetros `?`. */
export const perfilRepositorio = {
  /** Lista os perfis do aparelho, do mais antigo para o mais novo. */
  async listar(db: SQLiteDatabase): Promise<Perfil[]> {
    const linhas = await db.getAllAsync<LinhaPerfil>('SELECT * FROM perfil ORDER BY criado_em ASC');
    return linhas.map(paraPerfil);
  },

  /** Busca um perfil pelo id do entregador, ou `null`. */
  async obter(db: SQLiteDatabase, entregadorId: string): Promise<Perfil | null> {
    const linha = await db.getFirstAsync<LinhaPerfil>(
      'SELECT * FROM perfil WHERE entregador_id = ?',
      entregadorId,
    );
    return linha ? paraPerfil(linha) : null;
  },

  /**
   * Grava o perfil. Se o entregador já existir no aparelho, atualiza o apelido e o capacete
   * e mantém o `voluntario_id` e o `criado_em` que já estavam lá.
   */
  async salvar(db: SQLiteDatabase, perfil: Perfil): Promise<void> {
    await db.runAsync(
      `INSERT INTO perfil (entregador_id, apelido, codigo_capacete, voluntario_id, criado_em)
       VALUES (?, ?, ?, ?, ?)
       ON CONFLICT (entregador_id) DO UPDATE SET
         apelido = excluded.apelido,
         codigo_capacete = excluded.codigo_capacete`,
      perfil.entregadorId,
      perfil.apelido,
      perfil.codigoCapacete,
      perfil.voluntarioId,
      perfil.criadoEm,
    );
  },

  /** Troca o capacete e o apelido do perfil (depois do `POST /dispositivos`). */
  async atualizarCapacete(
    db: SQLiteDatabase,
    entregadorId: string,
    codigoCapacete: string | null,
    apelido: string,
  ): Promise<void> {
    await db.runAsync(
      'UPDATE perfil SET codigo_capacete = ?, apelido = ? WHERE entregador_id = ?',
      codigoCapacete,
      apelido,
      entregadorId,
    );
  },

  /** Guarda o id de voluntário do perfil (usado na próxima entrega). */
  async atualizarVoluntario(
    db: SQLiteDatabase,
    entregadorId: string,
    voluntarioId: string | null,
  ): Promise<void> {
    await db.runAsync(
      'UPDATE perfil SET voluntario_id = ? WHERE entregador_id = ?',
      voluntarioId,
      entregadorId,
    );
  },

  /** Remove o perfil do aparelho (não apaga nada na API). */
  async remover(db: SQLiteDatabase, entregadorId: string): Promise<void> {
    await db.runAsync('DELETE FROM perfil WHERE entregador_id = ?', entregadorId);
  },
};
