import Constants from 'expo-constants';

import { CAMINHO_API, PORTA_API } from './constantes';

/** De onde veio a URL da API em uso, para a tela Configurações mostrar. */
export type OrigemUrlApi = 'manual' | 'ambiente' | 'computador' | 'emulador';

/** URL base da API já resolvida, com a origem dela. */
export interface UrlApiResolvida {
  url: string;
  origem: OrigemUrlApi;
}

/** Texto simples de cada origem, para a interface. */
export const DESCRICAO_ORIGEM_URL: Record<OrigemUrlApi, string> = {
  manual: 'Endereço salvo por você',
  ambiente: 'Arquivo .env do projeto',
  computador: 'Detectado pelo computador que roda o Expo',
  emulador: 'Padrão do emulador Android',
};

/** Endereço que o emulador Android usa para chegar ao computador. */
const URL_EMULADOR = `http://10.0.2.2:${PORTA_API}${CAMINHO_API}`;

/** Tira espaços e barras do fim, para o Axios montar as rotas sem barra dupla. */
export function normalizarUrl(url: string): string {
  return url.trim().replace(/\/+$/, '');
}

/**
 * Descobre o IP do computador pelo `hostUri` do Metro (ex.: "192.168.0.10:8081").
 * No Expo Go, o celular baixou o app desse endereço, então a API está no mesmo IP.
 */
function urlPeloComputador(): string | null {
  const hostUri = Constants.expoConfig?.hostUri;
  if (!hostUri) {
    return null;
  }
  // Fica só com o host, sem a porta do Metro e sem caminho.
  const host = hostUri.split('/')[0]?.split(':')[0];
  if (!host) {
    return null;
  }
  return `http://${host}:${PORTA_API}${CAMINHO_API}`;
}

/**
 * Resolve a URL base da API na ordem do CLAUDE.md (seção "Rede"):
 * 1. a URL salva na tela Configurações;
 * 2. a variável `EXPO_PUBLIC_API_URL`;
 * 3. o IP do computador pelo `hostUri` do Metro;
 * 4. o endereço do emulador Android.
 */
export function resolverUrlApi(urlManual: string | null): UrlApiResolvida {
  if (urlManual && urlManual.trim() !== '') {
    return { url: normalizarUrl(urlManual), origem: 'manual' };
  }
  // O Expo troca `process.env.EXPO_PUBLIC_*` pelo valor do .env na hora de montar o app.
  const doAmbiente = process.env.EXPO_PUBLIC_API_URL;
  if (doAmbiente && doAmbiente.trim() !== '') {
    return { url: normalizarUrl(doAmbiente), origem: 'ambiente' };
  }
  const doComputador = urlPeloComputador();
  if (doComputador) {
    return { url: doComputador, origem: 'computador' };
  }
  return { url: URL_EMULADOR, origem: 'emulador' };
}
