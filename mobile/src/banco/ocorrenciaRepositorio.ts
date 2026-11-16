import type { SQLiteDatabase } from 'expo-sqlite';

import type { EstadoOcorrencia } from '@/tipos/contrato';

/** Papel do perfil local na ocorrência: quem caiu ou quem foi ajudar. */
export type PapelOcorrencia = 'VITIMA' | 'VOLUNTARIO';

/**
 * Ocorrência vista pelo perfil local, para o histórico offline. Sem latitude e longitude:
 * a localização de pessoas nunca vai para o SQLite (CLAUDE.md, "Banco local").
 */
export interface OcorrenciaLocal {
  id: string;
  /** Entregador do perfil local que viu a ocorrência (vítima ou voluntário). */
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

/** Repositório da tabela `ocorrencia`. Todas as consultas usam parâmetros `?`. */
export const ocorrenciaRepositorio = {
  /** Grava a ocorrência ou atualiza a que já existe com o mesmo id. */
  async salvar(db: SQLiteDatabase, ocorrencia: OcorrenciaLocal): Promise<void> {
    await db.runAsync(
      `INSERT INTO ocorrencia (id, entregador_id, papel, estado, camada, criada_em, aceita_em,
         encerrada_em, tempo_ate_aceite_segundos, atualizado_em)
       VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
       ON CONFLICT (id) DO UPDATE SET
         estado = excluded.estado,
         camada = excluded.camada,
         aceita_em = excluded.aceita_em,
         encerrada_em = excluded.encerrada_em,
         tempo_ate_aceite_segundos = excluded.tempo_ate_aceite_segundos,
         atualizado_em = excluded.atualizado_em`,
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
  },

  /** Ocorrências vistas pelo entregador, da mais recente para a mais antiga. */
  async listar(db: SQLiteDatabase, entregadorId: string): Promise<OcorrenciaLocal[]> {
    const linhas = await db.getAllAsync<LinhaOcorrencia>(
      'SELECT * FROM ocorrencia WHERE entregador_id = ? ORDER BY criada_em DESC',
      entregadorId,
    );
    return linhas.map(paraOcorrencia);
  },
};
