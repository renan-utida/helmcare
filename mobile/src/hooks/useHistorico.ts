import { useSQLiteContext } from 'expo-sqlite';
import { useCallback, useState } from 'react';

import { alertaRepositorio, type AlertaLocal } from '@/banco/alertaRepositorio';
import { ocorrenciaRepositorio, type OcorrenciaLocal } from '@/banco/ocorrenciaRepositorio';
import { turnoRepositorio, type TurnoLocal } from '@/banco/turnoRepositorio';

/** Dados do histórico de um perfil, todos lidos do SQLite. */
export interface DadosHistorico {
  /** De qual entregador são os dados (a troca de perfil nunca mostra o histórico de outro). */
  entregadorId: string;
  turnos: TurnoLocal[];
  alertas: AlertaLocal[];
  ocorrencias: OcorrenciaLocal[];
}

/** Resumo do histórico: o indicador de impacto do HelmCare (tempo médio até o aceite). */
export interface ResumoHistorico {
  turnos: number;
  ocorrencias: number;
  /** Ocorrências com aceite (as que entram na média). */
  comAceite: number;
  /** Média do `tempoAteAceiteSegundos` das que tiveram aceite, em segundos inteiros, ou `null`. */
  mediaAceiteSegundos: number | null;
}

/** O que o hook entrega para a aba Histórico. */
export interface EstadoHistorico {
  /** Dados do perfil informado, ou `null` enquanto a primeira leitura não termina. */
  dados: DadosHistorico | null;
  /** Lê de novo o SQLite (ao abrir a aba e no "puxar para atualizar"). */
  recarregar: () => Promise<void>;
}

/**
 * Histórico offline do perfil: turnos, mudanças de estado do capacete e ocorrências, lidos só
 * do SQLite (funciona sem rede). Nenhuma linha tem localização ou dado pessoal.
 */
export function useHistorico(entregadorId: string | null): EstadoHistorico {
  const db = useSQLiteContext();
  const [leitura, setLeitura] = useState<DadosHistorico | null>(null);

  const recarregar = useCallback(async () => {
    if (!entregadorId) {
      return;
    }
    const [turnos, alertas, ocorrencias] = await Promise.all([
      turnoRepositorio.listarDoEntregador(db, entregadorId),
      alertaRepositorio.listar(db, entregadorId),
      ocorrenciaRepositorio.listar(db, entregadorId),
    ]);
    setLeitura({ entregadorId, turnos, alertas, ocorrencias });
  }, [db, entregadorId]);

  const dados = leitura && leitura.entregadorId === entregadorId ? leitura : null;
  return { dados, recarregar };
}

/**
 * Monta o resumo: quantos turnos e ocorrências e o tempo médio até o aceite nas que tiveram
 * aceite. As encerradas sem aceite contam como "sem aceite" e ficam fora da média (seção 7).
 */
export function resumirHistorico(dados: DadosHistorico): ResumoHistorico {
  const tempos = dados.ocorrencias
    .map((ocorrencia) => ocorrencia.tempoAteAceiteSegundos)
    .filter((tempo): tempo is number => tempo !== null);
  const soma = tempos.reduce((total, tempo) => total + tempo, 0);
  return {
    turnos: dados.turnos.length,
    ocorrencias: dados.ocorrencias.length,
    comAceite: tempos.length,
    mediaAceiteSegundos: tempos.length > 0 ? Math.floor(soma / tempos.length) : null,
  };
}
