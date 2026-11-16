import { create } from 'axios';

import { TEMPO_LIMITE_REQUISICAO_MS } from '@/config/constantes';
import { resolverUrlApi, type UrlApiResolvida } from '@/config/urlApi';

import { lerToken } from './armazenamentoSeguro';
import { converterErro } from './erros';

/** URL em uso agora. Começa sem a URL manual; o SessaoContext aplica a salva ao abrir o app. */
let urlEmUso: UrlApiResolvida = resolverUrlApi(null);

/**
 * Instância única do Axios para a API principal (o app nunca chama o Serviço de Risco).
 * Todo erro sai daqui já convertido em `ErroApi`, então os serviços e as telas
 * só lidam com o tipo do erro, nunca com o objeto do Axios.
 */
export const api = create({
  baseURL: urlEmUso.url,
  timeout: TEMPO_LIMITE_REQUISICAO_MS,
  headers: { 'Content-Type': 'application/json' },
});

/**
 * Ponto único de autenticação (preparação para o login da API, contrato v1.15).
 * Requisição: com token salvo no armazenamento seguro, acrescenta "Authorization: Bearer".
 * Hoje nenhum token é salvo, então nada muda. O token nunca vai para log nem para URL.
 */
api.interceptors.request.use(async (config) => {
  const token = await lerToken();
  if (token) {
    config.headers.set('Authorization', `Bearer ${token}`);
  }
  return config;
});

/** Quem usa o app registra aqui o que fazer num 401 (por exemplo, voltar para o login). */
let tratadorNaoAutorizado: (() => void) | null = null;

/** Registra (ou remove, com `null`) o tratador do 401. Hoje ninguém registra. */
export function definirTratadorNaoAutorizado(tratador: (() => void) | null): void {
  tratadorNaoAutorizado = tratador;
}

api.interceptors.response.use(
  (resposta) => resposta,
  (erro: unknown) => {
    const erroApi = converterErro(erro);
    if (erroApi.tipo === 'nao-autorizado' && tratadorNaoAutorizado) {
      tratadorNaoAutorizado();
    }
    return Promise.reject(erroApi);
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
