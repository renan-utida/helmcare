import { useSQLiteContext } from 'expo-sqlite';
import { useCallback, useLayoutEffect, useRef, useState } from 'react';

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
 * Ocorrência vista pela conta ativa: `GET /ocorrencias/{id}` a cada 5 s (usePolling) até ela
 * terminar, e cada leitura atualiza a tabela `ocorrencia` (sem latitude e longitude). A vítima
 * consulta sem `voluntarioId` (localização aproximada, que a tela nem mostra). O voluntário manda
 * o próprio `voluntarioId`, e a localização vem exata se foi ele quem aceitou (seção 7): ela fica
 * só no estado da tela, nunca no SQLite nem em log.
 */
export function useOcorrencia(id: string, papel: PapelOcorrencia): EstadoOcorrenciaTela {
  const db = useSQLiteContext();
  const { contaAtiva, sessao } = useSessao();
  const entregadorId = contaAtiva;
  const voluntarioDaSessao = sessao && sessao.entregadorId === contaAtiva ? sessao.voluntarioId : null;
  const voluntarioId = papel === 'VOLUNTARIO' ? (voluntarioDaSessao ?? undefined) : undefined;
  // Conta em uso numa ref: uma resposta que chega depois da troca de conta é ignorada.
  const contaAtual = useRef(contaAtiva);
  useLayoutEffect(() => {
    contaAtual.current = contaAtiva;
  }, [contaAtiva]);
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
      const conta = contaAtual.current;
      const resposta = await ocorrenciaServico.obter(id, voluntarioId);
      if (contaAtual.current !== conta) {
        return;
      }
      await aplicar(resposta);
    } catch (e) {
      setErro(converterErro(e));
    }
  }, [aplicar, id, voluntarioId]);

  const terminou = ocorrencia !== null && ESTADOS_FINAIS.includes(ocorrencia.estado);
  // A chave junta a conta, a ocorrência e o papel: na troca de conta, o polling anterior para.
  usePolling(recarregar, !terminou && entregadorId !== null, `${entregadorId ?? ''}:${id}:${voluntarioId ?? 'vitima'}`);

  return { ocorrencia, lidoEm, erro, recarregar, aplicar };
}
