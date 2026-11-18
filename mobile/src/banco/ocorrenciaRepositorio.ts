import type { SQLiteDatabase } from 'expo-sqlite';

import type { EstadoOcorrencia } from '@/tipos/contrato';

/** Papel da conta na ocorrência: quem caiu ou quem foi ajudar. */
export type PapelOcorrencia = 'VITIMA' | 'VOLUNTARIO';

/**
 * Ocorrência vista por uma conta do aparelho, para o histórico offline. Sem latitude e longitude:
 * a localização de pessoas nunca vai para o SQLite (CLAUDE.md, "Banco local").
 */
export interface OcorrenciaLocal {
  id: string;
  /** Entregador da conta que viu a ocorrência (vítima ou voluntário). */
  entregadorId: string;
  papel: PapelOcorrencia;
  estado: EstadoOcorrencia;
  camada: number;
  criadaEm: string;
  aceitaEm: string | null;
  encerradaEm: string | null;
  tempoAteAceiteSegundos: number | null;
  atualizadoEm: string;
}

/** Linha da tabela `ocorrencia`, com os nomes das colunas. */
interface LinhaOcorrencia {
  id: string;
  entregador_id: string;
  papel: PapelOcorrencia;
  estado: EstadoOcorrencia;
  camada: number;
  criada_em: string;
  aceita_em: string | null;
  encerrada_em: string | null;
  tempo_ate_aceite_segundos: number | null;
  atualizado_em: string;
}

/** Converte a linha do SQLite no objeto usado pelo app. */
function paraOcorrencia(linha: LinhaOcorrencia): OcorrenciaLocal {
  return {
    id: linha.id,
    entregadorId: linha.entregador_id,
    papel: linha.papel,
    estado: linha.estado,
    camada: linha.camada,
    criadaEm: linha.criada_em,
    aceitaEm: linha.aceita_em,
    encerradaEm: linha.encerrada_em,
    tempoAteAceiteSegundos: linha.tempo_ate_aceite_segundos,
    atualizadoEm: linha.atualizado_em,
  };
}

/** Estados em que a ocorrência ainda está em andamento (antes de ENCERRADA ou CANCELADA). */
const ESTADOS_EM_ANDAMENTO = ['SOS_CAMADA_1', 'SOS_CAMADA_2', 'SOCORRO_A_CAMINHO'] as const;

/** Repositório da tabela `ocorrencia`. Todas as consultas usam parâmetros `?`. */
export const ocorrenciaRepositorio = {
  /**
   * Grava a ocorrência do perfil (se ainda não existir para esse entregador) e atualiza o estado
   * em TODOS os registros locais com o mesmo id. Com um celular só, a vítima e o voluntário são
   * duas contas do aparelho (migração 2: um registro para cada); quando o voluntário encerra, o
   * registro da vítima também passa a ENCERRADA, e o histórico dos dois fica certo sem rede.
   */
  async salvar(db: SQLiteDatabase, ocorrencia: OcorrenciaLocal): Promise<void> {
    // Sem transação de propósito: no expo-sqlite ela não é exclusiva, e o polling do alerta pode
    // gravar ao mesmo tempo. Também não é preciso: o INSERT já leva os valores atuais completos,
    // então, se o UPDATE falhar, nenhum registro fica com um estado misturado.
    await db.runAsync(
      `INSERT INTO ocorrencia (id, entregador_id, papel, estado, camada, criada_em, aceita_em,
         encerrada_em, tempo_ate_aceite_segundos, atualizado_em)
       VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
       ON CONFLICT (id, entregador_id) DO NOTHING`,
      ocorrencia.id,
      ocorrencia.entregadorId,
      ocorrencia.papel,
      ocorrencia.estado,
      ocorrencia.camada,
      ocorrencia.criadaEm,
      ocorrencia.aceitaEm,
      ocorrencia.encerradaEm,
      ocorrencia.tempoAteAceiteSegundos,
      ocorrencia.atualizadoEm,
    );
    await db.runAsync(
      `UPDATE ocorrencia SET estado = ?, camada = ?, aceita_em = ?, encerrada_em = ?,
         tempo_ate_aceite_segundos = ?, atualizado_em = ?
       WHERE id = ?`,
      ocorrencia.estado,
      ocorrencia.camada,
      ocorrencia.aceitaEm,
      ocorrencia.encerradaEm,
      ocorrencia.tempoAteAceiteSegundos,
      ocorrencia.atualizadoEm,
      ocorrencia.id,
    );
  },

  /** Ocorrências vistas pelo entregador, da mais recente para a mais antiga. */
  async listar(db: SQLiteDatabase, entregadorId: string): Promise<OcorrenciaLocal[]> {
    const linhas = await db.getAllAsync<LinhaOcorrencia>(
      'SELECT * FROM ocorrencia WHERE entregador_id = ? ORDER BY criada_em DESC',
      entregadorId,
    );
    return linhas.map(paraOcorrencia);
  },

  /**
   * Ocorrência que o entregador aceitou como voluntário e que ainda está em andamento pelo
   * último estado salvo, a mais recente, ou `null`. Usada para reabrir o atendimento e para
   * avisar antes da troca de conta.
   */
  async obterAceitaEmAndamento(db: SQLiteDatabase, entregadorId: string): Promise<OcorrenciaLocal | null> {
    const linha = await db.getFirstAsync<LinhaOcorrencia>(
      `SELECT * FROM ocorrencia
       WHERE entregador_id = ? AND papel = 'VOLUNTARIO' AND estado IN (?, ?, ?)
       ORDER BY criada_em DESC LIMIT 1`,
      entregadorId,
      ...ESTADOS_EM_ANDAMENTO,
    );
    return linha ? paraOcorrencia(linha) : null;
  },
};
