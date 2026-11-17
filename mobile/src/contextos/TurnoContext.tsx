import { router, usePathname, type Href } from 'expo-router';
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

import { alertaRepositorio } from '@/banco/alertaRepositorio';
import { turnoRepositorio, type TurnoLocal } from '@/banco/turnoRepositorio';
import { usePolling } from '@/hooks/usePolling';
import { converterErro } from '@/servicos/erros';
import { turnoServico } from '@/servicos/turnoServico';
import type { AlertaTurnoResposta, EstadoDispositivo, RiscoTurnoResposta } from '@/tipos/contrato';
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
  /**
   * Última leitura do `GET /turnos/{id}/alerta` do turno aberto, ou `null` (sem turno, turno
   * sem proteção ou ainda sem leitura). Consultado a cada 5 s com a proteção ligada.
   */
  alerta: AlertaTurnoResposta | null;
  /** Verdadeiro quando a última consulta do alerta não chegou à API (sem rede). */
  alertaSemConexao: boolean;
  /**
   * Instante (relógio do app, em ms) da primeira leitura com `estadoCapacete` = CONTAGEM neste
   * alerta, ou `null` fora da contagem. A contagem regressiva do app parte daqui (seção 7).
   */
  contagemVistaEm: number | null;
  /** Emergência em andamento no turno: alerta de queda ou ocorrência (SOS) da vítima. */
  emergencia: Emergencia | null;
  /** Consulta o alerta na hora, fora do ritmo do polling (ex.: logo depois de cancelar). */
  atualizarAlerta: () => Promise<void>;
}

/** Emergência do turno, para avisar antes de encerrar o turno ou trocar de perfil. */
export type Emergencia = { tipo: 'alerta' } | { tipo: 'ocorrencia'; ocorrenciaId: string };

/** Estados do capacete que abrem a tela de alerta (queda em verificação ou contagem). */
export const ESTADOS_DE_ALERTA: readonly EstadoDispositivo[] = ['IMPACTO_DETECTADO', 'OBSERVACAO', 'CONTAGEM'];

/** Confere se o estado do capacete é de alerta em andamento. */
export function ehEstadoDeAlerta(estado: EstadoDispositivo | null | undefined): boolean {
  return estado !== null && estado !== undefined && ESTADOS_DE_ALERTA.includes(estado);
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
 * Com a proteção ligada, consulta o alerta do capacete a cada 5 s e abre sozinho a tela
 * de alerta e a da ocorrência, de qualquer aba (por isso fica na raiz do app).
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

  // ---------------------------------------------------------------------------
  // Alerta de queda: polling, histórico local e abertura automática das telas
  // ---------------------------------------------------------------------------

  const caminho = usePathname();
  const caminhoAtual = useRef(caminho);
  useLayoutEffect(() => {
    caminhoAtual.current = caminho;
  }, [caminho]);

  // A leitura guarda de qual turno ela é, para nunca mostrar o alerta de um turno anterior.
  const [leitura, setLeitura] = useState<{ turnoId: string; resposta: AlertaTurnoResposta } | null>(null);
  const [alertaSemConexao, setAlertaSemConexao] = useState(false);
  const [contagem, setContagem] = useState<{ turnoId: string; vistaEm: number } | null>(null);
  const estadoAnterior = useRef<EstadoDispositivo | null>(null);
  // Ocorrências já abertas nesta sessão do app: o SOS_ENVIADO se repete até o capacete
  // receber o ENCERRAR, e a tela da ocorrência não pode reabrir a cada consulta.
  const ocorrenciasAbertas = useRef(new Set<string>());

  /** Reage a uma nova leitura: histórico, contagem e telas. */
  const tratarLeitura = useCallback(
    async (turnoId: string, resposta: AlertaTurnoResposta) => {
      const anterior = estadoAnterior.current;
      const atual = resposta.estadoCapacete;
      estadoAnterior.current = atual;

      if (atual) {
        await alertaRepositorio.registrar(db, turnoId, atual);
      }

      // A contagem do app parte da PRIMEIRA leitura em CONTAGEM e zera ao sair dela.
      if (atual === 'CONTAGEM' && anterior !== 'CONTAGEM') {
        setContagem({ turnoId, vistaEm: Date.now() });
      } else if (atual !== 'CONTAGEM') {
        setContagem(null);
      }

      // Abre o alerta de qualquer aba quando o capacete ENTRA num estado de alerta.
      if (ehEstadoDeAlerta(atual) && !ehEstadoDeAlerta(anterior) && caminhoAtual.current !== '/alerta') {
        router.push('/alerta');
      }

      // SOS enviado: abre a ocorrência da vítima (trocando a tela do alerta, se ela estiver aberta).
      const ocorrenciaId = resposta.ocorrenciaId;
      if (atual === 'SOS_ENVIADO' && ocorrenciaId && !ocorrenciasAbertas.current.has(ocorrenciaId)) {
        ocorrenciasAbertas.current.add(ocorrenciaId);
        const destino: Href = { pathname: '/ocorrencia/[id]', params: { id: ocorrenciaId, papel: 'VITIMA' } };
        if (caminhoAtual.current === '/alerta') {
          router.replace(destino);
        } else if (!caminhoAtual.current.startsWith('/ocorrencia/')) {
          router.push(destino);
        }
      }
    },
    [db],
  );

  const consultarAlerta = useCallback(async () => {
    const aberto = turnoAtual.current;
    if (!aberto || !aberto.protecaoAtiva) {
      return;
    }
    try {
      const resposta = await turnoServico.obterAlerta(aberto.id);
      setAlertaSemConexao(false);
      // O turno pode ter sido encerrado durante a consulta: só vale se ainda for o mesmo.
      if (turnoAtual.current?.id === aberto.id) {
        setLeitura({ turnoId: aberto.id, resposta });
        await tratarLeitura(aberto.id, resposta);
      }
    } catch (e) {
      // Sem rede, a tela mantém a última leitura e avisa; outros erros esperam a próxima consulta.
      setAlertaSemConexao(converterErro(e).tipo === 'sem-conexao');
    }
  }, [tratarLeitura]);

  // Polling do alerta só com turno aberto e proteção ligada (sem capacete, os campos vêm nulos).
  // A chave é o id do turno: na troca de perfil ou de turno, o polling anterior para na hora.
  usePolling(consultarAlerta, turno !== null && turno.protecaoAtiva, turno?.id ?? null);

  // Ao trocar de turno (encerrar ou trocar de perfil), esquece o estado anterior.
  const turnoId = turno?.id ?? null;
  useEffect(() => {
    estadoAnterior.current = null;
  }, [turnoId]);

  const alerta = leitura && turno && leitura.turnoId === turno.id ? leitura.resposta : null;
  const contagemVistaEm = contagem && turno && contagem.turnoId === turno.id ? contagem.vistaEm : null;

  const emergencia = useMemo<Emergencia | null>(() => {
    if (!alerta) {
      return null;
    }
    if (ehEstadoDeAlerta(alerta.estadoCapacete)) {
      return { tipo: 'alerta' };
    }
    if (alerta.estadoCapacete === 'SOS_ENVIADO' && alerta.ocorrenciaId) {
      return { tipo: 'ocorrencia', ocorrenciaId: alerta.ocorrenciaId };
    }
    return null;
  }, [alerta]);

  const valor = useMemo<ValorTurno>(
    () => ({
      turno,
      carregando,
      iniciar,
      atualizarRisco,
      encerrar,
      alerta,
      alertaSemConexao,
      contagemVistaEm,
      emergencia,
      atualizarAlerta: consultarAlerta,
    }),
    [
      turno,
      carregando,
      iniciar,
      atualizarRisco,
      encerrar,
      alerta,
      alertaSemConexao,
      contagemVistaEm,
      emergencia,
      consultarAlerta,
    ],
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
