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

/**
 * Migração 2: a chave da `ocorrencia` passa a ser (id, entregador_id). Com um celular só, a
 * vítima e o voluntário são duas contas do mesmo aparelho e veem a MESMA ocorrência: com a
 * chave só no id, o registro do voluntário caía no da vítima e sumia do histórico dele.
 * O SQLite não troca a chave de uma tabela, então ela é recriada e os dados são copiados.
 */
const MIGRACAO_2 = `
CREATE TABLE ocorrencia_v2 (
  id TEXT NOT NULL,
  entregador_id TEXT NOT NULL,
  papel TEXT NOT NULL CHECK (papel IN ('VITIMA', 'VOLUNTARIO')),
  estado TEXT NOT NULL,
  camada INTEGER NOT NULL,
  criada_em TEXT NOT NULL,
  aceita_em TEXT NULL,
  encerrada_em TEXT NULL,
  tempo_ate_aceite_segundos INTEGER NULL,
  atualizado_em TEXT NOT NULL,
  PRIMARY KEY (id, entregador_id)
);

INSERT INTO ocorrencia_v2 SELECT id, entregador_id, papel, estado, camada, criada_em, aceita_em,
  encerrada_em, tempo_ate_aceite_segundos, atualizado_em FROM ocorrencia;

DROP TABLE ocorrencia;

ALTER TABLE ocorrencia_v2 RENAME TO ocorrencia;

CREATE INDEX ix_ocorrencia_entregador_criada ON ocorrencia (entregador_id, criada_em);
`;

/**
 * Migração 3 (login da API, contrato 1.15): a identidade passa a ser o token da conta, guardado no
 * expo-secure-store, e não o perfil local.
 * - A tabela `perfil` sai: o apelido deixa de existir (o nome vem da sessão, só em memória) e o
 *   capacete e o voluntário vêm do `GET /entregadores/sessao`.
 * - A preferência `perfil_ativo` sai; o tema e a URL manual continuam.
 * - O histórico (turno, alerta e ocorrência) não muda: ele é ligado só pelo `entregador_id` (o
 *   alerta pelo `turno_id`), sem chave estrangeira para o perfil, e volta quando a conta do mesmo
 *   entregador entra.
 * - A ocorrência ganha `encerrada_automaticamente` (0 ou 1), para o histórico mostrar quando a API
 *   encerrou o SOS por tempo. Não é dado pessoal.
 */
const MIGRACAO_3 = `
DROP TABLE IF EXISTS perfil;

DELETE FROM preferencia WHERE chave = 'perfil_ativo';

ALTER TABLE ocorrencia ADD COLUMN encerrada_automaticamente INTEGER NOT NULL DEFAULT 0;
`;

/** Lista de migrações, em ordem. A próxima entra no fim, com a versão seguinte. */
const MIGRACOES: readonly Migracao[] = [
  { versao: 1, sql: MIGRACAO_1 },
  { versao: 2, sql: MIGRACAO_2 },
  { versao: 3, sql: MIGRACAO_3 },
];

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
