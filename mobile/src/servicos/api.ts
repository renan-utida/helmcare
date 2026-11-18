import { create, isAxiosError, type InternalAxiosRequestConfig } from 'axios';

import { TEMPO_LIMITE_REQUISICAO_MS } from '@/config/constantes';
import { resolverUrlApi, type UrlApiResolvida } from '@/config/urlApi';
import type { LoginEntregadorResposta } from '@/tipos/contrato';

import { apagarTokens, atualizarTokens, lerContaAtiva, lerTokens } from './armazenamentoSeguro';
import { converterErro, ErroApi } from './erros';

/** Opções do HelmCare em cada requisição (ficam só na memória da requisição, nunca em log). */
export interface OpcoesRequisicaoHelmcare {
  /**
   * Conta que faz a requisição. Sem ela, o interceptor usa a conta ativa e grava aqui qual foi,
   * para a renovação e a repetição usarem sempre a mesma conta, mesmo que a pessoa troque de
   * conta no meio. Dá para pedir outra conta de propósito (o nome de cada conta em "Contas").
   */
  entregadorId?: string;
  /** Rota pública (login, cadastro, renovação): vai sem Authorization e nunca renova. */
  publica?: boolean;
  /** A requisição já é a repetição depois de uma renovação: um novo 401 não renova de novo. */
  repetida?: boolean;
}

// Acrescenta o campo `helmcare` na configuração do Axios (declaration merging), sem `any`.
declare module 'axios' {
  interface AxiosRequestConfig {
    helmcare?: OpcoesRequisicaoHelmcare;
  }
}

/** URL em uso agora. Começa sem a URL manual; o SessaoContext aplica a salva ao abrir o app. */
let urlEmUso: UrlApiResolvida = resolverUrlApi(null);

/**
 * Instância única do Axios para a API principal (o app nunca chama o Serviço de Risco).
 * Todo erro sai daqui já convertido em `ErroApi`, então os serviços e as telas
 * só lidam com o tipo do erro, nunca com o objeto do Axios (que tem o Authorization).
 */
export const api = create({
  baseURL: urlEmUso.url,
  timeout: TEMPO_LIMITE_REQUISICAO_MS,
  headers: { 'Content-Type': 'application/json' },
});

/**
 * Autenticação (contrato 1.15, seção 7): toda rota que não é pública leva
 * "Authorization: Bearer <accessToken>" da conta que faz a requisição. Conta sem tokens (a sessão
 * terminou) nem envia: falha na hora com 401, sem renovar, para o polling não insistir na API.
 */
api.interceptors.request.use(async (config) => {
  const opcoes = config.helmcare ?? {};
  if (opcoes.publica) {
    return config;
  }
  const conta = opcoes.entregadorId ?? (await lerContaAtiva());
  if (!conta) {
    return config;
  }
  config.helmcare = { ...opcoes, entregadorId: conta };
  const tokens = await lerTokens(conta);
  if (!tokens) {
    throw new ErroApi('nao-autorizado', 401);
  }
  config.headers.set('Authorization', `Bearer ${tokens.accessToken}`);
  return config;
});

/** Quem usa o app registra aqui o que fazer quando a sessão de uma conta termina. */
let tratadorSessaoTerminada: ((entregadorId: string) => void) | null = null;

/**
 * Registra (ou remove, com `null`) o tratador do fim da sessão: chamado uma vez quando a
 * renovação de uma conta responde 401. O SessaoContext decide o que mostrar (A6).
 */
export function definirTratadorNaoAutorizado(tratador: ((entregadorId: string) => void) | null): void {
  tratadorSessaoTerminada = tratador;
}

/** Renovações em andamento, uma por conta: quem leva 401 ao mesmo tempo espera a mesma. */
const renovacoesEmAndamento = new Map<string, Promise<void>>();

/**
 * Troca o `refreshToken` da conta por tokens novos (`POST /entregadores/renovar-token`) e os salva.
 * 401 na renovação: a sessão dessa conta terminou, os tokens dela são apagados e o tratador é
 * chamado. Sem conexão, 429 ou 5xx: a conta continua salva e o erro sobe como sempre.
 */
async function renovar(entregadorId: string): Promise<void> {
  const tokens = await lerTokens(entregadorId);
  if (!tokens) {
    throw new ErroApi('nao-autorizado', 401);
  }
  try {
    const { data } = await api.post<LoginEntregadorResposta>(
      '/entregadores/renovar-token',
      { refreshToken: tokens.refreshToken },
      { helmcare: { publica: true } },
    );
    await atualizarTokens(entregadorId, { accessToken: data.accessToken, refreshToken: data.refreshToken });
  } catch (e) {
    const erro = converterErro(e);
    if (erro.tipo === 'nao-autorizado') {
      await apagarTokens(entregadorId);
      tratadorSessaoTerminada?.(entregadorId);
    }
    throw erro;
  }
}

/** Renova a conta uma vez só, reaproveitando a renovação que já estiver em andamento. */
function renovarUmaVez(entregadorId: string): Promise<void> {
  const emAndamento = renovacoesEmAndamento.get(entregadorId);
  if (emAndamento) {
    return emAndamento;
  }
  const promessa = renovar(entregadorId).finally(() => renovacoesEmAndamento.delete(entregadorId));
  renovacoesEmAndamento.set(entregadorId, promessa);
  return promessa;
}

/** Token que a requisição levou no Authorization, para saber se outra chamada já renovou. */
function tokenEnviado(config: InternalAxiosRequestConfig): string | null {
  const valor = config.headers.get('Authorization');
  return typeof valor === 'string' ? valor.replace(/^Bearer /, '') : null;
}

api.interceptors.response.use(
  (resposta) => resposta,
  async (erro: unknown) => {
    const erroApi = converterErro(erro);
    // Só o primeiro 401 de uma rota que não é pública (login, cadastro e renovação) tenta renovar.
    if (erroApi.tipo !== 'nao-autorizado' || !isAxiosError(erro) || !erro.config) {
      throw erroApi;
    }
    const config = erro.config;
    const opcoes = config.helmcare;
    const conta = opcoes?.entregadorId;
    if (!conta || opcoes.publica || opcoes.repetida) {
      throw erroApi;
    }
    // Se outra chamada já renovou enquanto esta estava no ar, só repete com o token novo.
    const atuais = await lerTokens(conta);
    if (!atuais || atuais.accessToken === tokenEnviado(config)) {
      await renovarUmaVez(conta);
    }
    // Repete a chamada original uma vez, com a mesma conta, marcada para não renovar de novo.
    return api.request({ ...config, helmcare: { ...opcoes, repetida: true } });
  },
);

/**
 * Aplica a URL manual salva nos Ajustes (ou `null` para voltar à automática)
 * e troca a URL base do Axios. Devolve a URL resolvida e a origem dela.
 */
export function aplicarUrlManual(urlManual: string | null): UrlApiResolvida {
  urlEmUso = resolverUrlApi(urlManual);
  api.defaults.baseURL = urlEmUso.url;
  return urlEmUso;
}

/** URL da API em uso agora e a origem dela, para a aba Ajustes. */
export function obterUrlEmUso(): UrlApiResolvida {
  return urlEmUso;
}
