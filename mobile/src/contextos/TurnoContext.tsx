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
import { ALERTA_CALOR_C } from '@/config/constantes';
import { usePolling } from '@/hooks/usePolling';
import { converterErro } from '@/servicos/erros';
import { notificacoes } from '@/servicos/notificacoes';
import { turnoServico } from '@/servicos/turnoServico';
import type { AlertaTurnoResposta, EstadoDispositivo, RiscoTurnoResposta, TurnoResposta } from '@/tipos/contrato';
import { agoraIso, somarHorasIso } from '@/utils/datas';

import { useSessao } from './SessaoContext';

/** Como terminou o `encerrar`: na API (200) ou só no aparelho (404, turno que a API não tem). */
export type ResultadoEncerrar = 'encerrado' | 'encerrado-so-no-aparelho';

/**
 * Turno aberto da conta ativa, como a tela usa. Quase sempre é o turno do SQLite. Quando a sessão
 * traz um `turnoAtivoId` que o SQLite não tem (turno aberto em outro aparelho ou antes do login),
 * ele é restaurado só em memória (`soEmMemoria`): o `GET /turnos/{id}/risco` traz só o Índice,
 * então os horários e a proteção ficam `null` até o encerrar, cujo 200 traz tudo e vai para o
 * histórico. `indiceDisponivel` é falso enquanto o Índice de um turno restaurado não chegou.
 */
export interface TurnoAtual extends Omit<TurnoLocal, 'inicio' | 'fimPrevisto' | 'protecaoAtiva'> {
  inicio: string | null;
  fimPrevisto: string | null;
  protecaoAtiva: boolean | null;
  soEmMemoria: boolean;
  indiceDisponivel: boolean;
}

/** O que o contexto do turno entrega para as telas. */
export interface ValorTurno {
  /** Turno aberto da conta ativa, ou `null`. */
  turno: TurnoAtual | null;
  /** Verdadeiro até a primeira leitura do SQLite (e da sessão, se ela estiver carregando). */
  carregando: boolean;
  /**
   * Inicia o turno (`POST /turnos`) com início agora e fim em `horas`, grava no SQLite e
   * devolve o turno com o Índice. Lança `ErroApi` (409, 503, 400, sem conexão) para a tela.
   */
  iniciar: (horas: number, protecaoAtiva: boolean) => Promise<TurnoAtual | null>;
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
  /**
   * Instante (relógio do app, em ms) da primeira leitura em IMPACTO_DETECTADO ou OBSERVACAO neste
   * alerta, ou `null` fora da verificação. O anel de `JANELA_OBSERVACAO_S` parte daqui.
   */
  verificacaoVistaEm: number | null;
  /** Emergência em andamento no turno: alerta de queda ou ocorrência (SOS) da vítima. */
  emergencia: Emergencia | null;
  /** Consulta o alerta na hora, fora do ritmo do polling (ex.: logo depois de cancelar). */
  atualizarAlerta: () => Promise<void>;
}

/** Emergência do turno, para avisar antes de encerrar o turno ou trocar de conta. */
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

/** Turno do SQLite no formato da tela. */
function deLocal(local: TurnoLocal): TurnoAtual {
  return { ...local, soEmMemoria: false, indiceDisponivel: true };
}

/** Turno restaurado pela sessão, sem horários e sem a proteção, com o Índice (se chegou). */
function restaurado(id: string, entregadorId: string, risco: RiscoTurnoResposta | null): TurnoAtual {
  return {
    id,
    entregadorId,
    inicio: null,
    fimPrevisto: null,
    fim: null,
    protecaoAtiva: null,
    indiceRisco: risco?.indiceRisco ?? 0,
    faixaRisco: risco?.faixaRisco ?? 'BAIXO',
    componenteSinistro: risco?.componentes.sinistro ?? 0,
    componenteChuva: risco?.componentes.chuva ?? 0,
    componenteCalor: risco?.componentes.calor ?? 0,
    explicacao: risco?.explicacao ?? '',
    atualizadoEm: agoraIso(),
    soEmMemoria: true,
    indiceDisponivel: risco !== null,
  };
}

/** Turno completo do 200 do encerrar (ele traz os horários, a proteção e o Índice). */
function doEncerrar(resposta: TurnoResposta): TurnoLocal {
  return comRisco(
    {
      id: resposta.turnoId,
      entregadorId: resposta.entregadorId,
      inicio: resposta.inicio,
      fimPrevisto: resposta.fimPrevisto,
      fim: resposta.fim ?? agoraIso(),
      protecaoAtiva: resposta.protecaoAtiva,
    },
    resposta,
  );
}

/**
 * Turno da conta ativa. Junta a API (fonte da verdade, pela sessão) e o SQLite (para mostrar o
 * turno e o último Índice sem conexão e para o histórico). As telas não tocam no SQLite.
 * Com a proteção ligada, consulta o alerta do capacete a cada 5 s e abre sozinho a tela
 * de alerta e a da ocorrência, de qualquer aba (por isso fica na raiz do app).
 */
export function TurnoProvider({ children }: { children: ReactNode }) {
  const db = useSQLiteContext();
  const { contaAtiva, sessao, estadoSessao } = useSessao();
  const entregadorId = contaAtiva;
  // Sessão desta conta (a da conta anterior nunca vale para esta).
  const sessaoDaConta = sessao && sessao.entregadorId === entregadorId ? sessao : null;
  // `undefined`: a sessão ainda não chegou (ou não vai chegar, sem conexão).
  const turnoAtivoDaApi = sessaoDaConta ? sessaoDaConta.turnoAtivoId : undefined;
  const temCapacete = sessaoDaConta?.codigoDispositivo != null;
  // O turno guarda de qual entregador ele é, para a troca de conta nunca mostrar o turno de outro.
  const [estado, setEstado] = useState<{ entregadorId: string | null; turno: TurnoAtual | null } | null>(null);

  // Lê o turno aberto do SQLite e concilia com a sessão. A API manda: sem `turnoAtivoId`, o turno
  // local é encerrado no histórico; com outro id, o local é encerrado e o da API é restaurado.
  // Sem sessão (sem conexão), fica o que o SQLite tem.
  useEffect(() => {
    let ativo = true;
    (async () => {
      if (!entregadorId) {
        if (ativo) setEstado({ entregadorId, turno: null });
        return;
      }
      const local = await turnoRepositorio.obterAtivo(db, entregadorId);
      if (turnoAtivoDaApi === undefined) {
        if (ativo) setEstado({ entregadorId, turno: local ? deLocal(local) : null });
        return;
      }
      if (local && local.id !== turnoAtivoDaApi) {
        await turnoRepositorio.marcarEncerrado(db, local.id, agoraIso());
      }
      if (turnoAtivoDaApi === null) {
        if (ativo) setEstado({ entregadorId, turno: null });
        return;
      }
      if (local && local.id === turnoAtivoDaApi) {
        if (ativo) setEstado({ entregadorId, turno: deLocal(local) });
        return;
      }
      let risco: RiscoTurnoResposta | null = null;
      try {
        risco = await turnoServico.obterRisco(turnoAtivoDaApi);
      } catch {
        // Sem o Índice agora: o turno aparece mesmo assim e o Índice chega no próximo foco da aba.
      }
      if (ativo) setEstado({ entregadorId, turno: restaurado(turnoAtivoDaApi, entregadorId, risco) });
    })();
    return () => {
      ativo = false;
    };
  }, [db, entregadorId, turnoAtivoDaApi]);

  // Enquanto a sessão carrega, espera por ela para não mostrar um turno que a API já fechou.
  const carregando =
    estado === null ||
    estado.entregadorId !== entregadorId ||
    (estadoSessao === 'carregando' && turnoAtivoDaApi === undefined);
  const turno = carregando || !estado ? null : estado.turno;

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
        const atual = deLocal(novo);
        setEstado({ entregadorId, turno: atual });
        return atual;
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
  const turnoAtual = useRef<TurnoAtual | null>(turno);
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
      const { inicio, fimPrevisto, protecaoAtiva } = aberto;
      let atualizado: TurnoAtual;
      if (inicio !== null && fimPrevisto !== null && protecaoAtiva !== null) {
        const local = comRisco({ ...aberto, inicio, fimPrevisto, protecaoAtiva }, risco);
        await turnoRepositorio.salvar(db, local);
        atualizado = deLocal(local);
      } else {
        // Turno restaurado: sem os horários, ele não vai para o SQLite até o encerrar.
        atualizado = restaurado(aberto.id, aberto.entregadorId, risco);
      }
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
      // O 200 traz o turno inteiro: vale também para o restaurado, que entra no histórico agora.
      await turnoRepositorio.salvar(db, doEncerrar(resposta));
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
  const [verificacao, setVerificacao] = useState<{ turnoId: string; vistaEm: number } | null>(null);
  // Turnos em que o calor forte já virou notificação: uma vez só por turno.
  const calorNotificado = useRef(new Set<string>());
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

      // Calor forte medido pelo capacete (maior ou igual a ALERTA_CALOR_C): notifica uma vez por
      // turno, quando passa a valer. O aviso na tela continua no Turno em andamento.
      if (
        resposta.indiceCalorC !== null &&
        resposta.indiceCalorC >= ALERTA_CALOR_C &&
        !calorNotificado.current.has(turnoId)
      ) {
        calorNotificado.current.add(turnoId);
        void notificacoes.calorForte();
      }

      // A contagem do app parte da PRIMEIRA leitura em CONTAGEM e zera ao sair dela.
      // A verificação (impacto e observação) também é contada no relógio do app, para o anel.
      const emVerificacao = atual === 'IMPACTO_DETECTADO' || atual === 'OBSERVACAO';
      const estavaEmVerificacao = anterior === 'IMPACTO_DETECTADO' || anterior === 'OBSERVACAO';
      if (emVerificacao && !estavaEmVerificacao) {
        setVerificacao({ turnoId, vistaEm: Date.now() });
      } else if (!emVerificacao) {
        setVerificacao(null);
      }

      if (atual === 'CONTAGEM' && anterior !== 'CONTAGEM') {
        setContagem({ turnoId, vistaEm: Date.now() });
        // Com o app minimizado, é a notificação que avisa (o polling do alerta continua).
        void notificacoes.alertaDeQueda();
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
        void notificacoes.sosEnviado(ocorrenciaId);
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
    if (!aberto || aberto.protecaoAtiva === false) {
      return;
    }
    try {
      const resposta = await turnoServico.obterAlerta(aberto.id);
      setAlertaSemConexao(false);
      // O turno pode ter sido encerrado (ou a conta trocada) durante a consulta: só vale se
      // ainda for o mesmo turno. Uma resposta atrasada da conta anterior é ignorada.
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
  // No turno restaurado, a proteção não é conhecida: consulta se a conta tem capacete (o estado
  // do capacete diz se ele está armado). A chave junta a conta e o turno: na troca de conta ou de
  // turno, o polling anterior para na hora.
  const consultaAlerta =
    turno !== null && (turno.protecaoAtiva === true || (turno.protecaoAtiva === null && temCapacete));
  // Único polling que continua com o app minimizado (melhor esforço: o Android pode pausar o app).
  usePolling(consultarAlerta, consultaAlerta, turno ? `${turno.entregadorId}:${turno.id}` : null, {
    continuarEmSegundoPlano: true,
  });

  // Ocorrência ativa como vítima trazida pela sessão (o app foi fechado no meio de um SOS): abre a
  // tela da ocorrência uma vez, depois que a abertura do app terminou (fora da rota "/" e do login).
  const ocorrenciaDaSessao = sessaoDaConta?.ocorrenciaAtivaId ?? null;
  useEffect(() => {
    if (!ocorrenciaDaSessao || ocorrenciasAbertas.current.has(ocorrenciaDaSessao)) {
      return;
    }
    if (caminho === '/' || caminho === '/login' || caminho.startsWith('/ocorrencia/')) {
      return;
    }
    ocorrenciasAbertas.current.add(ocorrenciaDaSessao);
    router.push({ pathname: '/ocorrencia/[id]', params: { id: ocorrenciaDaSessao, papel: 'VITIMA' } });
  }, [ocorrenciaDaSessao, caminho]);

  // Ao trocar de turno (encerrar ou trocar de conta), esquece o estado anterior.
  const turnoId = turno?.id ?? null;
  useEffect(() => {
    estadoAnterior.current = null;
  }, [turnoId]);

  const alerta = leitura && turno && leitura.turnoId === turno.id ? leitura.resposta : null;
  const contagemVistaEm = contagem && turno && contagem.turnoId === turno.id ? contagem.vistaEm : null;
  const verificacaoVistaEm = verificacao && turno && verificacao.turnoId === turno.id ? verificacao.vistaEm : null;

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
      verificacaoVistaEm,
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
      verificacaoVistaEm,
      emergencia,
      consultarAlerta,
    ],
  );

  return <TurnoContext.Provider value={valor}>{children}</TurnoContext.Provider>;
}

/** Acessa o turno da conta ativa. Só funciona dentro do `TurnoProvider`. */
export function useTurno(): ValorTurno {
  const valor = useContext(TurnoContext);
  if (!valor) {
    throw new Error('useTurno precisa estar dentro do TurnoProvider.');
  }
  return valor;
}
