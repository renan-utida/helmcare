import { router, usePathname } from 'expo-router';
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

import { CHAVES_PREFERENCIA, preferenciaRepositorio } from '@/banco/preferenciaRepositorio';
import { MAXIMO_CONTAS_APARELHO } from '@/config/constantes';
import type { UrlApiResolvida } from '@/config/urlApi';
import { aplicarUrlManual, definirTratadorNaoAutorizado, obterUrlEmUso } from '@/servicos/api';
import {
  definirContaAtiva,
  lerContaAtiva,
  lerTokens,
  listarContas,
  removerConta,
  salvarConta,
} from '@/servicos/armazenamentoSeguro';
import { autenticacaoServico } from '@/servicos/autenticacaoServico';
import { converterErro } from '@/servicos/erros';
import type { LoginEntregadorResposta, SessaoEntregadorResposta } from '@/tipos/contrato';

/**
 * Situação da sessão da conta ativa:
 * - `sem-conta`: nenhuma conta no aparelho (vai para o login);
 * - `carregando`: o `GET /entregadores/sessao` está no ar;
 * - `ok`: a sessão chegou;
 * - `sem-conexao`: a API não respondeu; o app abre com a conta salva, sem nome;
 * - `terminada`: a renovação deu 401 (ou a conta não tem mais tokens): é preciso entrar de novo;
 * - `erro`: outra falha (por exemplo, 5xx); o app segue com a conta salva.
 */
export type EstadoSessao = 'sem-conta' | 'carregando' | 'ok' | 'sem-conexao' | 'terminada' | 'erro';

/** Telas em que o fim da sessão não tira a pessoa da tela (alerta de queda e SOS). */
function telaDeEmergencia(caminho: string): boolean {
  return caminho === '/alerta' || caminho.startsWith('/ocorrencia/');
}

/** O que a sessão entrega para as telas. */
export interface ValorSessao {
  /** Ids das contas logadas no aparelho (até `MAXIMO_CONTAS_APARELHO`), na ordem em que entraram. */
  contas: string[];
  /** Contas da lista cuja sessão terminou (sem tokens): elas precisam entrar de novo. */
  contasSemSessao: string[];
  /** Id do entregador da conta em uso, ou `null`. É a identidade do app (antes, o perfil local). */
  contaAtiva: string | null;
  /**
   * Sessão da conta ativa (`GET /entregadores/sessao`), SÓ em memória: nome, voluntário,
   * capacete, turno e ocorrência abertos. `null` enquanto carrega, sem conexão ou sem conta.
   */
  sessao: SessaoEntregadorResposta | null;
  estadoSessao: EstadoSessao;
  /** Verdadeiro até a primeira leitura do secure-store e das preferências terminar. */
  carregando: boolean;
  /** URL da API em uso e a origem dela. */
  urlApi: UrlApiResolvida;
  /** Verdadeiro quando cabe mais uma conta (ou a conta informada já está no aparelho). */
  podeAdicionarConta: (entregadorId?: string) => boolean;
  /**
   * Guarda a conta do login (tokens só no secure-store), a deixa em uso e usa a sessão que veio
   * na própria resposta. Os tokens nunca vão para o estado do React.
   */
  adicionarConta: (login: LoginEntregadorResposta) => Promise<void>;
  /** Troca para outra conta salva, sem senha. */
  trocarConta: (entregadorId: string) => Promise<void>;
  /**
   * "Sair": apaga só os tokens dessa conta (o histórico local fica) e, se era a conta em uso, passa
   * para outra conta salva. Devolve a conta em uso depois (ou `null`: vai para o login).
   * Sem rota de logout no contrato, o refreshToken continua válido no servidor até vencer.
   */
  sair: (entregadorId: string) => Promise<string | null>;
  /** Lê de novo a sessão da conta ativa (depois de virar voluntário ou vincular capacete). */
  recarregarSessao: () => Promise<SessaoEntregadorResposta | null>;
  /** Salva a URL manual da API (ou `null` para voltar à automática) e passa a usá-la. */
  definirUrlManual: (url: string | null) => Promise<void>;
}

const SessaoContext = createContext<ValorSessao | null>(null);

/** Sessão sem os campos de token, para guardar no estado. */
function semTokens(login: LoginEntregadorResposta): SessaoEntregadorResposta {
  return {
    entregadorId: login.entregadorId,
    nome: login.nome,
    voluntarioId: login.voluntarioId,
    codigoDispositivo: login.codigoDispositivo,
    turnoAtivoId: login.turnoAtivoId,
    ocorrenciaAtivaId: login.ocorrenciaAtivaId,
  };
}

/**
 * Sessão do aparelho (contrato 1.15): as contas logadas ficam no expo-secure-store e a identidade é
 * o token da conta ativa. A sessão (nome, voluntário, capacete, turno e ocorrência ativos) vem do
 * `GET /entregadores/sessao` e fica só em memória. Também registra o tratador do fim da sessão:
 * no alerta e na ocorrência, a tela fica e mostra a faixa "Entre de novo"; nas outras, vai para o login.
 */
export function SessaoProvider({ children }: { children: ReactNode }) {
  const db = useSQLiteContext();
  const [contas, setContas] = useState<string[]>([]);
  const [contasSemSessao, setContasSemSessao] = useState<string[]>([]);
  const [contaAtiva, setContaAtiva] = useState<string | null>(null);
  // A sessão guarda de qual conta ela é: uma resposta atrasada da conta anterior é ignorada.
  const [leitura, setLeitura] = useState<{
    entregadorId: string;
    estado: EstadoSessao;
    sessao: SessaoEntregadorResposta | null;
  } | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [urlApi, setUrlApi] = useState<UrlApiResolvida>(obterUrlEmUso());

  const contaAtual = useRef(contaAtiva);
  useLayoutEffect(() => {
    contaAtual.current = contaAtiva;
  }, [contaAtiva]);

  const caminho = usePathname();
  const caminhoAtual = useRef(caminho);
  useLayoutEffect(() => {
    caminhoAtual.current = caminho;
  }, [caminho]);

  /** Lê do secure-store a lista de contas e quais estão sem tokens. */
  const lerContas = useCallback(async () => {
    const lista = await listarContas();
    const semSessao: string[] = [];
    for (const id of lista) {
      if (!(await lerTokens(id))) {
        semSessao.push(id);
      }
    }
    setContas(lista);
    setContasSemSessao(semSessao);
    return lista;
  }, []);

  /** Busca a sessão de uma conta e guarda o resultado (só se ela ainda for a conta em uso). */
  const buscarSessao = useCallback(async (entregadorId: string): Promise<SessaoEntregadorResposta | null> => {
    try {
      const sessao = await autenticacaoServico.obterSessao(entregadorId);
      if (contaAtual.current === entregadorId) {
        setLeitura({ entregadorId, estado: 'ok', sessao });
      }
      return sessao;
    } catch (e) {
      const erro = converterErro(e);
      const estado: EstadoSessao =
        erro.tipo === 'sem-conexao' ? 'sem-conexao' : erro.tipo === 'nao-autorizado' ? 'terminada' : 'erro';
      if (contaAtual.current === entregadorId) {
        setLeitura({ entregadorId, estado, sessao: null });
      }
      return null;
    }
  }, []);

  // Primeira carga: URL manual da API, contas do secure-store e a sessão da conta ativa.
  useEffect(() => {
    let ativo = true;
    (async () => {
      const urlManual = await preferenciaRepositorio.obter(db, CHAVES_PREFERENCIA.urlApiManual);
      const resolvida = aplicarUrlManual(urlManual);
      const lista = await lerContas();
      let ativa = await lerContaAtiva();
      // Conta ativa que sumiu da lista: usa a primeira, para não mandar ao login à toa.
      if (!ativa || !lista.includes(ativa)) {
        ativa = lista[0] ?? null;
        await definirContaAtiva(ativa);
      }
      if (!ativo) {
        return;
      }
      setUrlApi(resolvida);
      contaAtual.current = ativa;
      setContaAtiva(ativa);
      setCarregando(false);
    })();
    // Evita trocar o estado depois que o provedor saiu da tela.
    return () => {
      ativo = false;
    };
  }, [db, lerContas]);

  // Sessão da conta ativa: ao abrir o app e a cada troca de conta.
  useEffect(() => {
    if (!contaAtiva) {
      return;
    }
    let vivo = true;
    (async () => {
      // Espera um passo antes de mexer no estado: trocar estado direto no corpo do efeito
      // causa renderizações em cascata (regra react-hooks/set-state-in-effect).
      await Promise.resolve();
      if (vivo) {
        await buscarSessao(contaAtiva);
      }
    })();
    return () => {
      vivo = false;
    };
  }, [contaAtiva, buscarSessao]);

  // Fim da sessão (401 na renovação, avisado pelo api.ts).
  useEffect(() => {
    definirTratadorNaoAutorizado((entregadorId) => {
      setContasSemSessao((atuais) => (atuais.includes(entregadorId) ? atuais : [...atuais, entregadorId]));
      if (entregadorId !== contaAtual.current) {
        return;
      }
      setLeitura((atual) =>
        atual && atual.entregadorId === entregadorId ? { ...atual, estado: 'terminada' } : atual,
      );
      // No alerta e na ocorrência, a tela fica (faixa "Entre de novo" e o 192 visível). Nas
      // outras, vai para o login. Antes de terminar a abertura (rota "/"), quem decide é o index.
      const atual = caminhoAtual.current;
      if (!telaDeEmergencia(atual) && atual !== '/' && atual !== '/login') {
        router.replace({ pathname: '/login', params: { motivo: 'sessao' } });
      }
    });
    return () => definirTratadorNaoAutorizado(null);
  }, []);

  const podeAdicionarConta = useCallback(
    (entregadorId?: string) =>
      (entregadorId !== undefined && contas.includes(entregadorId)) || contas.length < MAXIMO_CONTAS_APARELHO,
    [contas],
  );

  const adicionarConta = useCallback(
    async (login: LoginEntregadorResposta) => {
      await salvarConta(login.entregadorId, { accessToken: login.accessToken, refreshToken: login.refreshToken });
      await definirContaAtiva(login.entregadorId);
      await lerContas();
      contaAtual.current = login.entregadorId;
      // A sessão que veio no login já vale; o efeito da conta ativa confirma com o GET da sessão.
      setLeitura({ entregadorId: login.entregadorId, estado: 'ok', sessao: semTokens(login) });
      setContaAtiva(login.entregadorId);
    },
    [lerContas],
  );

  const trocarConta = useCallback(async (entregadorId: string) => {
    await definirContaAtiva(entregadorId);
    contaAtual.current = entregadorId;
    setContaAtiva(entregadorId);
  }, []);

  const sair = useCallback(
    async (entregadorId: string) => {
      await removerConta(entregadorId);
      const restantes = await lerContas();
      if (entregadorId !== contaAtual.current) {
        return contaAtual.current;
      }
      const proxima = restantes[0] ?? null;
      await definirContaAtiva(proxima);
      contaAtual.current = proxima;
      setLeitura(null);
      setContaAtiva(proxima);
      return proxima;
    },
    [lerContas],
  );

  const recarregarSessao = useCallback(async () => {
    const conta = contaAtual.current;
    return conta ? buscarSessao(conta) : null;
  }, [buscarSessao]);

  const definirUrlManual = useCallback(
    async (url: string | null) => {
      if (url) {
        await preferenciaRepositorio.definir(db, CHAVES_PREFERENCIA.urlApiManual, url);
      } else {
        await preferenciaRepositorio.remover(db, CHAVES_PREFERENCIA.urlApiManual);
      }
      setUrlApi(aplicarUrlManual(url));
    },
    [db],
  );

  // Só vale a leitura da conta em uso; sem leitura dela ainda, está carregando.
  const daContaAtiva = leitura && leitura.entregadorId === contaAtiva ? leitura : null;
  const estadoSessao: EstadoSessao = !contaAtiva
    ? 'sem-conta'
    : contasSemSessao.includes(contaAtiva)
      ? 'terminada'
      : (daContaAtiva?.estado ?? 'carregando');
  const sessao = estadoSessao === 'ok' ? (daContaAtiva?.sessao ?? null) : null;

  const valor = useMemo<ValorSessao>(
    () => ({
      contas,
      contasSemSessao,
      contaAtiva,
      sessao,
      estadoSessao,
      carregando,
      urlApi,
      podeAdicionarConta,
      adicionarConta,
      trocarConta,
      sair,
      recarregarSessao,
      definirUrlManual,
    }),
    [
      contas,
      contasSemSessao,
      contaAtiva,
      sessao,
      estadoSessao,
      carregando,
      urlApi,
      podeAdicionarConta,
      adicionarConta,
      trocarConta,
      sair,
      recarregarSessao,
      definirUrlManual,
    ],
  );

  return <SessaoContext.Provider value={valor}>{children}</SessaoContext.Provider>;
}

/** Acessa a sessão do aparelho. Só funciona dentro do `SessaoProvider`. */
export function useSessao(): ValorSessao {
  const valor = useContext(SessaoContext);
  if (!valor) {
    throw new Error('useSessao precisa estar dentro do SessaoProvider.');
  }
  return valor;
}
