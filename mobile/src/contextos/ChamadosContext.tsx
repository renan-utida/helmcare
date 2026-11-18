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
import { AccessibilityInfo, Vibration } from 'react-native';

import { ocorrenciaRepositorio, type OcorrenciaLocal } from '@/banco/ocorrenciaRepositorio';
import { usePolling } from '@/hooks/usePolling';
import { converterErro, type ErroApi } from '@/servicos/erros';
import { ocorrenciaServico } from '@/servicos/ocorrenciaServico';
import { voluntarioServico } from '@/servicos/voluntarioServico';
import type { OcorrenciaProximaResposta, OcorrenciaResposta } from '@/tipos/contrato';
import { agoraIso } from '@/utils/datas';
import { posicaoSimulada, type IdPosicaoSimulada } from '@/utils/posicoesSimuladas';

import { useSessao } from './SessaoContext';

/** Frase anunciada (e vibrada) quando aparece um chamado que o perfil ainda não tinha visto. */
export const ANUNCIO_NOVO_CHAMADO = 'Novo chamado de socorro perto de você';

/** Vibração do chamado novo: duas batidas, para chamar a atenção sem assustar. */
const VIBRACAO_NOVO_CHAMADO = [0, 400, 200, 400];

/** Disponibilidade do voluntário neste aparelho, só em memória (nunca no SQLite). */
export interface EstadoRede {
  /** Último `disponivel` aceito pela API (200 do `PUT /voluntarios/{id}/disponibilidade`). */
  disponivel: boolean;
  /** Posição simulada escolhida, ou `null` (o app abriu sem posição em memória). */
  posicao: IdPosicaoSimulada | null;
}

/** Resultado de ligar a disponibilidade: sem posição escolhida, nada é enviado. */
export type ResultadoDisponibilidade = 'ok' | 'sem-posicao';

/** O que o contexto da rede de voluntários entrega para as telas. */
export interface ValorChamados {
  /** Id de voluntário do perfil ativo, ou `null` (ainda não faz parte da rede neste aparelho). */
  voluntarioId: string | null;
  /** Disponibilidade e posição do perfil ativo. Ao abrir o app, começa desligada e sem posição. */
  rede: EstadoRede;
  /** Chamados da última consulta, ou `null` (indisponível ou ainda sem leitura). */
  chamados: OcorrenciaProximaResposta[] | null;
  /** Instante (relógio do app, ms) da última leitura, para o cronômetro de cada chamado. */
  lidoEm: number | null;
  /** Erro da última consulta dos chamados (`null` quando ela deu certo). */
  erroChamados: ErroApi | null;
  /** Atendimento aceito pelo perfil ativo e ainda em andamento (pelo SQLite), ou `null`. */
  ocorrenciaAceita: OcorrenciaLocal | null;
  /** `POST /voluntarios` e grava o `voluntarioId` no perfil. Lança `ErroApi` (409, 404...). */
  participar: () => Promise<void>;
  /** Liga ou desliga a disponibilidade (`PUT`). Lança `ErroApi`. */
  definirDisponivel: (disponivel: boolean) => Promise<ResultadoDisponibilidade>;
  /** Escolhe a posição simulada; com a disponibilidade ligada (ou na primeira escolha), manda o `PUT`. */
  escolherPosicao: (id: IdPosicaoSimulada) => Promise<void>;
  /** `POST /ocorrencias/{id}/aceites`, grava a ocorrência (papel VOLUNTARIO). Lança `ErroApi`. */
  aceitar: (ocorrenciaId: string) => Promise<OcorrenciaResposta>;
  /** Consulta os chamados na hora (por exemplo, depois de um 409 no aceite). */
  recarregarChamados: () => Promise<void>;
  /** Relê do SQLite o atendimento em andamento (depois de encerrar, por exemplo). */
  atualizarOcorrenciaAceita: () => Promise<void>;
  /** Última `distanciaKm` vista para a ocorrência nesta sessão do app, ou `null`. */
  distanciaDe: (ocorrenciaId: string) => number | null;
}

const ChamadosContext = createContext<ValorChamados | null>(null);

/** Estado inicial da rede: desligada e sem posição (regra do pedido ao abrir o app). */
const REDE_INICIAL: EstadoRede = { disponivel: false, posicao: null };

/**
 * Rede de voluntários da conta ativa (seção 7, "Regras da rede de voluntários"). Fica na raiz
 * do app: com o perfil voluntário e disponível, consulta os chamados próximos a cada 5 s
 * (um polling só, usado pela aba Chamados e pelo número no ícone da aba) e, quando aparece um
 * chamado novo, vibra e anuncia. A posição simulada e a disponibilidade ficam só em memória,
 * por perfil: coordenada de pessoa nunca vai para o SQLite.
 */
export function ChamadosProvider({ children }: { children: ReactNode }) {
  const db = useSQLiteContext();
  const { contaAtiva, sessao, recarregarSessao } = useSessao();
  const entregadorId = contaAtiva;
  // O voluntarioId vem da sessão da conta ativa (contrato 1.15).
  const voluntarioId = sessao && sessao.entregadorId === entregadorId ? sessao.voluntarioId : null;

  // Disponibilidade por entregador: ao trocar de conta e voltar, a escolha anterior continua
  // (a API também continua com o último PUT). Só em memória: some ao fechar o app.
  const [redes, setRedes] = useState<Record<string, EstadoRede>>({});
  const rede = (entregadorId ? redes[entregadorId] : undefined) ?? REDE_INICIAL;

  // Cada leitura guarda de qual voluntário ela é, para a troca de conta nunca mostrar a lista de outro.
  const [leitura, setLeitura] = useState<{
    voluntarioId: string;
    lista: OcorrenciaProximaResposta[];
    lidoEm: number;
  } | null>(null);
  const [falha, setFalha] = useState<{ voluntarioId: string; erro: ErroApi } | null>(null);

  // Voluntário ativo numa ref: uma resposta que chega depois da troca de conta é descartada.
  const voluntarioAtual = useRef(voluntarioId);
  useLayoutEffect(() => {
    voluntarioAtual.current = voluntarioId;
  }, [voluntarioId]);

  // Chamados já vistos por este voluntário, para vibrar e anunciar só os novos.
  const vistos = useRef<{ voluntarioId: string | null; ids: Set<string> }>({ voluntarioId: null, ids: new Set() });
  // Distância de cada chamado (km, aproximada), para a tela do atendimento. Só em memória.
  const distancias = useRef(new Map<string, number>());

  const consultarChamados = useCallback(async () => {
    const id = voluntarioAtual.current;
    if (!id) {
      return;
    }
    try {
      const lista = await ocorrenciaServico.listarProximas(id);
      if (voluntarioAtual.current !== id) {
        return;
      }
      setLeitura({ voluntarioId: id, lista, lidoEm: Date.now() });
      setFalha(null);

      if (vistos.current.voluntarioId !== id) {
        vistos.current = { voluntarioId: id, ids: new Set() };
      }
      let novos = 0;
      for (const chamado of lista) {
        distancias.current.set(chamado.ocorrenciaId, chamado.distanciaKm);
        if (!vistos.current.ids.has(chamado.ocorrenciaId)) {
          vistos.current.ids.add(chamado.ocorrenciaId);
          novos += 1;
        }
      }
      if (novos > 0) {
        Vibration.vibrate(VIBRACAO_NOVO_CHAMADO);
        AccessibilityInfo.announceForAccessibility(ANUNCIO_NOVO_CHAMADO);
      }
    } catch (e) {
      if (voluntarioAtual.current === id) {
        setFalha({ voluntarioId: id, erro: converterErro(e) });
      }
    }
  }, []);

  // Um polling só, com a conta voluntária e disponível. A `chave` é o voluntarioId: na troca
  // de conta, o polling da conta anterior é cancelado e o da nova começa.
  usePolling(consultarChamados, voluntarioId !== null && rede.disponivel, voluntarioId);

  const chamados =
    voluntarioId && rede.disponivel && leitura?.voluntarioId === voluntarioId ? leitura.lista : null;
  const lidoEm = chamados && leitura ? leitura.lidoEm : null;
  const erroChamados = voluntarioId && rede.disponivel && falha?.voluntarioId === voluntarioId ? falha.erro : null;

  // Atendimento em andamento do perfil ativo, lido do SQLite (funciona sem rede).
  const [aceita, setAceita] = useState<{ entregadorId: string | null; ocorrencia: OcorrenciaLocal | null } | null>(
    null,
  );
  const atualizarOcorrenciaAceita = useCallback(async () => {
    const ocorrencia = entregadorId ? await ocorrenciaRepositorio.obterAceitaEmAndamento(db, entregadorId) : null;
    setAceita({ entregadorId, ocorrencia });
  }, [db, entregadorId]);

  useEffect(() => {
    let ativo = true;
    (async () => {
      const ocorrencia = entregadorId ? await ocorrenciaRepositorio.obterAceitaEmAndamento(db, entregadorId) : null;
      if (ativo) {
        setAceita({ entregadorId, ocorrencia });
      }
    })();
    return () => {
      ativo = false;
    };
  }, [db, entregadorId]);

  const ocorrenciaAceita = aceita?.entregadorId === entregadorId ? aceita.ocorrencia : null;

  /** Grava a disponibilidade do entregador na memória do app. */
  const guardarRede = useCallback((idEntregador: string, nova: EstadoRede) => {
    setRedes((atuais) => ({ ...atuais, [idEntregador]: nova }));
  }, []);

  const participar = useCallback(async () => {
    if (!entregadorId) {
      return;
    }
    try {
      await voluntarioServico.cadastrar(entregadorId);
    } catch (e) {
      // 409: já é voluntário. Com o login, o id vem na sessão: basta reler, sem erro na tela.
      if (converterErro(e).tipo !== 'conflito') {
        throw converterErro(e);
      }
    }
    // O 201 (ou o 409) muda a sessão: o voluntarioId passa a vir no GET /entregadores/sessao.
    await recarregarSessao();
    // A API cria o voluntário indisponível e sem posição: o app começa igual.
    guardarRede(entregadorId, REDE_INICIAL);
  }, [entregadorId, recarregarSessao, guardarRede]);

  const definirDisponivel = useCallback(
    async (disponivel: boolean): Promise<ResultadoDisponibilidade> => {
      if (!entregadorId || !voluntarioId) {
        return 'ok';
      }
      if (disponivel) {
        if (!rede.posicao) {
          return 'sem-posicao';
        }
        const { latitude, longitude } = posicaoSimulada(rede.posicao);
        await voluntarioServico.atualizarDisponibilidade(voluntarioId, { disponivel: true, latitude, longitude });
      } else {
        // Indisponível manda só `disponivel: false`; a API apaga a posição.
        await voluntarioServico.atualizarDisponibilidade(voluntarioId, { disponivel: false });
      }
      guardarRede(entregadorId, { disponivel, posicao: rede.posicao });
      return 'ok';
    },
    [entregadorId, voluntarioId, rede.posicao, guardarRede],
  );

  const escolherPosicao = useCallback(
    async (id: IdPosicaoSimulada) => {
      if (!entregadorId || !voluntarioId) {
        return;
      }
      // Com a disponibilidade ligada, ou na primeira escolha depois de abrir o app, a posição
      // nova vai para a API e liga a disponibilidade. Desligada de propósito, só guarda a escolha.
      if (rede.disponivel || rede.posicao === null) {
        const { latitude, longitude } = posicaoSimulada(id);
        await voluntarioServico.atualizarDisponibilidade(voluntarioId, { disponivel: true, latitude, longitude });
        guardarRede(entregadorId, { disponivel: true, posicao: id });
        // Posição nova muda a lista (raio da camada 1): consulta na hora.
        void consultarChamados();
      } else {
        guardarRede(entregadorId, { disponivel: false, posicao: id });
      }
    },
    [entregadorId, voluntarioId, rede, guardarRede, consultarChamados],
  );

  const aceitar = useCallback(
    async (ocorrenciaId: string) => {
      if (!entregadorId || !voluntarioId) {
        throw converterErro(new Error('Perfil sem voluntário'));
      }
      const resposta = await ocorrenciaServico.aceitar(ocorrenciaId, voluntarioId);
      const local: OcorrenciaLocal = {
        id: resposta.id,
        entregadorId,
        papel: 'VOLUNTARIO',
        estado: resposta.estado,
        camada: resposta.camada,
        criadaEm: resposta.criadaEm,
        aceitaEm: resposta.aceitaEm,
        encerradaEm: resposta.encerradaEm,
        tempoAteAceiteSegundos: resposta.tempoAteAceiteSegundos,
        encerradaAutomaticamente: resposta.encerradaAutomaticamente,
        atualizadoEm: agoraIso(),
      };
      // Sem latitude e longitude: a localização exata fica só na tela do atendimento.
      await ocorrenciaRepositorio.salvar(db, local);
      setAceita({ entregadorId, ocorrencia: local });
      // O chamado aceito sai da lista na hora (a próxima consulta já não o traz).
      setLeitura((atual) =>
        atual ? { ...atual, lista: atual.lista.filter((item) => item.ocorrenciaId !== ocorrenciaId) } : atual,
      );
      return resposta;
    },
    [db, entregadorId, voluntarioId],
  );

  const distanciaDe = useCallback((ocorrenciaId: string) => distancias.current.get(ocorrenciaId) ?? null, []);

  const valor = useMemo<ValorChamados>(
    () => ({
      voluntarioId,
      rede,
      chamados,
      lidoEm,
      erroChamados,
      ocorrenciaAceita,
      participar,
      definirDisponivel,
      escolherPosicao,
      aceitar,
      recarregarChamados: consultarChamados,
      atualizarOcorrenciaAceita,
      distanciaDe,
    }),
    [
      voluntarioId,
      rede,
      chamados,
      lidoEm,
      erroChamados,
      ocorrenciaAceita,
      participar,
      definirDisponivel,
      escolherPosicao,
      aceitar,
      consultarChamados,
      atualizarOcorrenciaAceita,
      distanciaDe,
    ],
  );

  return <ChamadosContext.Provider value={valor}>{children}</ChamadosContext.Provider>;
}

/** Acessa a rede de voluntários do perfil ativo. Só funciona dentro do `ChamadosProvider`. */
export function useChamados(): ValorChamados {
  const valor = useContext(ChamadosContext);
  if (!valor) {
    throw new Error('useChamados precisa estar dentro do ChamadosProvider.');
  }
  return valor;
}
