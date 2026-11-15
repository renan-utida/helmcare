import type { SQLiteDatabase } from 'expo-sqlite';

/**
 * Migrações do banco local, controladas por `PRAGMA user_version`.
 * Regra: uma migração já publicada nunca muda. Mudança nova vira a próxima da lista.
 *
 * O banco nunca guarda nome, CPF, CNH, telefone, contatos, ficha médica,
 * localização de pessoas, `qrToken` nem `urlCofre` (CLAUDE.md, "Banco local").
 */

/** Uma migração: o número da versão e o SQL que leva o banco até ela. */
interface Migracao {
  versao: number;
  sql: string;
}

/** Migração 1: as seis tabelas do app. */
const MIGRACAO_1 = `
CREATE TABLE perfil (
  entregador_id TEXT PRIMARY KEY NOT NULL,
  apelido TEXT NOT NULL,
  codigo_capacete TEXT NULL,
  voluntario_id TEXT NULL,
  criado_em TEXT NOT NULL
);

CREATE TABLE preferencia (
  chave TEXT PRIMARY KEY NOT NULL,
  valor TEXT NOT NULL
);

CREATE TABLE turno (
  id TEXT PRIMARY KEY NOT NULL,
  entregador_id TEXT NOT NULL,
  inicio TEXT NOT NULL,
  fim_previsto TEXT NOT NULL,
  fim TEXT NULL,
  protecao_ativa INTEGER NOT NULL,
  indice_risco INTEGER NOT NULL,
  faixa_risco TEXT NOT NULL,
  componente_sinistro INTEGER NOT NULL,
  componente_chuva INTEGER NOT NULL,
  componente_calor INTEGER NOT NULL,
  explicacao TEXT NOT NULL,
  atualizado_em TEXT NOT NULL
);

CREATE INDEX ix_turno_entregador_inicio ON turno (entregador_id, inicio);

CREATE TABLE alerta (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  turno_id TEXT NOT NULL,
  estado TEXT NOT NULL,
  registrado_em TEXT NOT NULL
);

CREATE TABLE ocorrencia (
  id TEXT PRIMARY KEY NOT NULL,
  entregador_id TEXT NOT NULL,
  papel TEXT NOT NULL CHECK (papel IN ('VITIMA', 'VOLUNTARIO')),
  estado TEXT NOT NULL,
  camada INTEGER NOT NULL,
  criada_em TEXT NOT NULL,
  aceita_em TEXT NULL,
  encerrada_em TEXT NULL,
  tempo_ate_aceite_segundos INTEGER NULL,
  atualizado_em TEXT NOT NULL
);

CREATE TABLE trecho_cache (
  posicao INTEGER PRIMARY KEY NOT NULL,
  logradouro TEXT NOT NULL,
  latitude REAL NOT NULL,
  longitude REAL NOT NULL,
  quantidade_sinistros_graves INTEGER NOT NULL,
  periodo TEXT NOT NULL,
  atualizado_em TEXT NOT NULL
);
`;

/** Lista de migrações, em ordem. A próxima entra no fim, com a versão seguinte. */
const MIGRACOES: readonly Migracao[] = [{ versao: 1, sql: MIGRACAO_1 }];

/**
 * Executa as migrações pendentes. Usada no `onInit` do `SQLiteProvider`, antes de qualquer tela.
 * Cada migração roda numa transação junto com a troca do `user_version`:
 * se falhar no meio, nada fica pela metade.
 */
export async function migrarBanco(db: SQLiteDatabase): Promise<void> {
  // WAL deixa leituras e escritas acontecerem juntas; não pode rodar dentro de transação.
  await db.execAsync('PRAGMA journal_mode = WAL;');

  const linha = await db.getFirstAsync<{ user_version: number }>('PRAGMA user_version');
  const versaoAtual = linha?.user_version ?? 0;

  for (const migracao of MIGRACOES) {
    if (migracao.versao <= versaoAtual) {
      continue;
    }
    await db.withTransactionAsync(async () => {
      await db.execAsync(migracao.sql);
      // O PRAGMA não aceita parâmetro `?`; o número vem da lista fixa acima, nunca do usuário.
      await db.execAsync(`PRAGMA user_version = ${migracao.versao}`);
    });
  }
}
