import { useSQLiteContext } from 'expo-sqlite';
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';

import { turnoRepositorio, type TurnoLocal } from '@/banco/turnoRepositorio';
import { converterErro } from '@/servicos/erros';
import { turnoServico } from '@/servicos/turnoServico';
import type { RiscoTurnoResposta } from '@/tipos/contrato';
import { agoraIso, somarHorasIso } from '@/utils/datas';

import { useSessao } from './SessaoContext';

/** Como terminou o `encerrar`: na API (200) ou só no aparelho (404, turno que a API não tem). */
export type ResultadoEncerrar = 'encerrado' | 'encerrado-so-no-aparelho';

/** O que o contexto do turno entrega para as telas. */
export interface ValorTurno {
  /** Turno aberto do perfil ativo, salvo no SQLite, ou `null`. */
  turno: TurnoLocal | null;
  /** Verdadeiro até a primeira leitura do SQLite para o perfil ativo. */
  carregando: boolean;
  /**
   * Inicia o turno (`POST /turnos`) com início agora e fim em `horas`, grava no SQLite e
   * devolve o turno com o Índice. Lança `ErroApi` (409, 503, 400, sem conexão) para a tela.
   */
  iniciar: (horas: number, protecaoAtiva: boolean) => Promise<TurnoLocal | null>;
  /** Atualiza o Índice pelo `GET /turnos/{id}/risco` e grava no SQLite. Lança `ErroApi`. */
  atualizarRisco: () => Promise<void>;
  /** Encerra o turno (`POST /turnos/{id}/encerrar`). Lança `ErroApi` no 409, 503 e sem conexão. */
  encerrar: () => Promise<ResultadoEncerrar>;
}

const TurnoContext = createContext<ValorTurno | null>(null);

/** Campos do turno que não vêm do Índice de Risco. */
type DadosBaseTurno = Pick<TurnoLocal, 'id' | 'entregadorId' | 'inicio' | 'fimPrevisto' | 'fim' | 'protecaoAtiva'>;

/** Monta o turno local juntando os dados do turno com o Índice devolvido pela API. */
function comRisco(base: DadosBaseTurno, risco: RiscoTurnoResposta): TurnoLocal {
  return {
    ...base,
    indiceRisco: risco.indiceRisco,
    faixaRisco: risco.faixaRisco,
    componenteSinistro: risco.componentes.sinistro,
    componenteChuva: risco.componentes.chuva,
    componenteCalor: risco.componentes.calor,
    explicacao: risco.explicacao,
    atualizadoEm: agoraIso(),
  };
}

/**
 * Turno do perfil ativo. Junta a API (fonte da verdade) e o SQLite (para mostrar o turno
 * e o último Índice sem conexão e para o histórico). As telas não tocam no SQLite.
 */
export function TurnoProvider({ children }: { children: ReactNode }) {
  const db = useSQLiteContext();
  const { perfilAtivo } = useSessao();
  const entregadorId = perfilAtivo?.entregadorId ?? null;
  // O turno guarda de qual entregador ele é, para a troca de perfil nunca mostrar o turno de outro.
  const [estado, setEstado] = useState<{ entregadorId: string | null; turno: TurnoLocal | null } | null>(null);

  // Lê o turno aberto sempre que o perfil ativo muda.
  useEffect(() => {
    let ativo = true;
    (async () => {
      const turno = entregadorId ? await turnoRepositorio.obterAtivo(db, entregadorId) : null;
      if (ativo) {
        setEstado({ entregadorId, turno });
      }
    })();
    return () => {
      ativo = false;
    };
  }, [db, entregadorId]);

  const carregando = estado === null || estado.entregadorId !== entregadorId;
  const turno = carregando ? null : estado.turno;

  const iniciar = useCallback(
    async (horas: number, protecaoAtiva: boolean) => {
      if (!entregadorId) {
        return null;
      }
      const inicio = new Date();
      const requisicao = {
        entregadorId,
        inicio: inicio.toISOString(),
        fimPrevisto: somarHorasIso(inicio, horas),
        protecaoAtiva,
      };
      try {
        const risco = await turnoServico.iniciar(requisicao);
        const novo = comRisco(
          {
            id: risco.turnoId,
            entregadorId,
            inicio: requisicao.inicio,
            fimPrevisto: requisicao.fimPrevisto,
            fim: null,
            protecaoAtiva,
          },
          risco,
        );
        await turnoRepositorio.salvar(db, novo);
        setEstado({ entregadorId, turno: novo });
        return novo;
      } catch (e) {
        throw converterErro(e);
      }
    },
    [db, entregadorId],
  );

  // Guarda o turno atual numa ref para o `atualizarRisco` não mudar a cada Índice novo:
  // assim a tela pode chamá-lo ao ganhar foco sem entrar em repetição. É `useLayoutEffect`
  // porque ele roda antes dos efeitos das telas; com `useEffect`, a tela recém-aberta
  // chamaria o `atualizarRisco` com a ref ainda vazia.
  const turnoAtual = useRef<TurnoLocal | null>(turno);
  useLayoutEffect(() => {
    turnoAtual.current = turno;
  }, [turno]);

  const atualizarRisco = useCallback(async () => {
    const aberto = turnoAtual.current;
    if (!aberto) {
      return;
    }
    try {
      const risco = await turnoServico.obterRisco(aberto.id);
      const atualizado = comRisco(aberto, risco);
      await turnoRepositorio.salvar(db, atualizado);
      // Só troca a tela se o turno ainda for o mesmo (o usuário pode ter encerrado no meio).
      if (turnoAtual.current?.id === aberto.id) {
        setEstado({ entregadorId: aberto.entregadorId, turno: atualizado });
      }
    } catch (e) {
      throw converterErro(e);
    }
  }, [db]);

  const encerrar = useCallback(async (): Promise<ResultadoEncerrar> => {
    if (!turno) {
      return 'encerrado';
    }
    try {
      const resposta = await turnoServico.encerrar(turno.id);
      await turnoRepositorio.marcarEncerrado(db, turno.id, resposta.fim ?? agoraIso());
      setEstado({ entregadorId: turno.entregadorId, turno: null });
      return 'encerrado';
    } catch (e) {
      const erro = converterErro(e);
      // 404: a API não conhece mais este turno (ex.: banco recriado). Fecha só no aparelho.
      if (erro.tipo === 'nao-encontrado') {
        await turnoRepositorio.marcarEncerrado(db, turno.id, agoraIso());
        setEstado({ entregadorId: turno.entregadorId, turno: null });
        return 'encerrado-so-no-aparelho';
      }
      throw erro;
    }
  }, [db, turno]);

  const valor = useMemo<ValorTurno>(
    () => ({ turno, carregando, iniciar, atualizarRisco, encerrar }),
    [turno, carregando, iniciar, atualizarRisco, encerrar],
  );

  return <TurnoContext.Provider value={valor}>{children}</TurnoContext.Provider>;
}

/** Acessa o turno do perfil ativo. Só funciona dentro do `TurnoProvider`. */
export function useTurno(): ValorTurno {
  const valor = useContext(TurnoContext);
  if (!valor) {
    throw new Error('useTurno precisa estar dentro do TurnoProvider.');
  }
  return valor;
}
