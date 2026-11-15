import { create } from 'axios';

import { TEMPO_LIMITE_REQUISICAO_MS } from '@/config/constantes';
import { resolverUrlApi, type UrlApiResolvida } from '@/config/urlApi';

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

api.interceptors.response.use(
  (resposta) => resposta,
  (erro: unknown) => Promise.reject(converterErro(erro)),
);

/**
 * Aplica a URL manual salva nas Configurações (ou `null` para voltar à automática)
 * e troca a URL base do Axios. Devolve a URL resolvida e a origem dela.
 */
export function aplicarUrlManual(urlManual: string | null): UrlApiResolvida {
  urlEmUso = resolverUrlApi(urlManual);
  api.defaults.baseURL = urlEmUso.url;
  return urlEmUso;
}

/** URL da API em uso agora e a origem dela, para a tela Configurações. */
export function obterUrlEmUso(): UrlApiResolvida {
  return urlEmUso;
}
