import { useSQLiteContext } from 'expo-sqlite';
import { useCallback, useState } from 'react';

import { ocorrenciaRepositorio, type PapelOcorrencia } from '@/banco/ocorrenciaRepositorio';
import { useSessao } from '@/contextos/SessaoContext';
import { converterErro, type ErroApi } from '@/servicos/erros';
import { ocorrenciaServico } from '@/servicos/ocorrenciaServico';
import type { EstadoOcorrencia, OcorrenciaResposta } from '@/tipos/contrato';

import { usePolling } from './usePolling';

/** Estados em que a ocorrência já terminou (o polling para). */
export const ESTADOS_FINAIS: readonly EstadoOcorrencia[] = ['ENCERRADA', 'CANCELADA'];

/** O que o hook entrega para a tela da ocorrência. */
export interface EstadoOcorrenciaTela {
  ocorrencia: OcorrenciaResposta | null;
  /** Instante (relógio do app, ms) em que a última resposta chegou, para o cronômetro. */
  lidoEm: number | null;
  /** Último erro da consulta (null quando a última deu certo). */
  erro: ErroApi | null;
  /** Consulta na hora. */
  recarregar: () => Promise<void>;
  /** Usa uma resposta que já veio de outra rota (cancelar ou encerrar) e grava no SQLite. */
  aplicar: (resposta: OcorrenciaResposta) => Promise<void>;
}

/**
 * Ocorrência vista pelo perfil local: `GET /ocorrencias/{id}` a cada 5 s (usePolling) até ela
 * terminar, e cada leitura atualiza a tabela `ocorrencia` (sem localização). A vítima consulta
 * sem `voluntarioId`, então a localização vem aproximada (a tela nem a mostra).
 */
export function useOcorrencia(id: string, papel: PapelOcorrencia): EstadoOcorrenciaTela {
  const db = useSQLiteContext();
  const { perfilAtivo } = useSessao();
  const entregadorId = perfilAtivo?.entregadorId ?? null;
  const [ocorrencia, setOcorrencia] = useState<OcorrenciaResposta | null>(null);
  const [lidoEm, setLidoEm] = useState<number | null>(null);
  const [erro, setErro] = useState<ErroApi | null>(null);

  const aplicar = useCallback(
    async (resposta: OcorrenciaResposta) => {
      setOcorrencia(resposta);
      setLidoEm(Date.now());
      setErro(null);
      if (entregadorId) {
        await ocorrenciaRepositorio.salvar(db, {
          id: resposta.id,
          entregadorId,
          papel,
          estado: resposta.estado,
          camada: resposta.camada,
          criadaEm: resposta.criadaEm,
          aceitaEm: resposta.aceitaEm,
          encerradaEm: resposta.encerradaEm,
          tempoAteAceiteSegundos: resposta.tempoAteAceiteSegundos,
          atualizadoEm: new Date().toISOString(),
        });
      }
    },
    [db, entregadorId, papel],
  );

  const recarregar = useCallback(async () => {
    try {
      await aplicar(await ocorrenciaServico.obter(id));
    } catch (e) {
      setErro(converterErro(e));
    }
  }, [aplicar, id]);

  const terminou = ocorrencia !== null && ESTADOS_FINAIS.includes(ocorrencia.estado);
  usePolling(recarregar, !terminou);

  return { ocorrencia, lidoEm, erro, recarregar, aplicar };
}
