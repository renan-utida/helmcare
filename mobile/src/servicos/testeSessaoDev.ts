import { atualizarTokens, lerTokens } from './armazenamentoSeguro';

/**
 * Ações de teste da sessão, SÓ no desenvolvimento (`__DEV__`), para ver a renovação e o fim da
 * sessão sem esperar os 60 min do accessToken. Fora do `__DEV__` não fazem nada, e a aba Ajustes
 * nem mostra o atalho. Os tokens trocados são textos inválidos fixos, nunca um token real.
 */

/** Texto que a API recusa como token (401), no lugar do token verdadeiro. */
const TOKEN_INVALIDO = 'token-invalido-de-teste';

/**
 * "Invalidar o token de acesso": troca só o accessToken salvo. A próxima chamada leva 401, o app
 * renova com o refreshToken verdadeiro e repete a chamada (o polling segue sem cair).
 */
export async function invalidarAcessoParaTeste(entregadorId: string): Promise<void> {
  if (!__DEV__) return;
  const tokens = await lerTokens(entregadorId);
  if (tokens) {
    await atualizarTokens(entregadorId, { accessToken: TOKEN_INVALIDO, refreshToken: tokens.refreshToken });
  }
}

/**
 * "Invalidar a renovação": troca os dois tokens. A próxima chamada leva 401, a renovação também,
 * e o app cai no tratador do fim da sessão (faixa no alerta e na ocorrência, login nas outras).
 */
export async function invalidarRenovacaoParaTeste(entregadorId: string): Promise<void> {
  if (!__DEV__) return;
  if (await lerTokens(entregadorId)) {
    await atualizarTokens(entregadorId, { accessToken: TOKEN_INVALIDO, refreshToken: TOKEN_INVALIDO });
  }
}
