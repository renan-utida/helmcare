import { converterErro } from '@/servicos/erros';

/** Novas tentativas depois do primeiro 409 (o contrato pede até 3). */
const TENTATIVAS_EXTRAS = 3;
/** Pausa curta entre as tentativas, em milissegundos. */
const PAUSA_MS = 500;

/**
 * Repete uma chamada que respondeu 409 (CLAUDE.md e seção 7 do contrato: o cancelar e o
 * encerrar da ocorrência podem dar 409 num conflito com o escalonamento da camada 2).
 * Faz a chamada e, em cada 409, espera uma pausa curta e tenta de novo, até 3 vezes.
 * Outros erros sobem na hora. Se o 409 continuar, lança o último erro: a tela relê a
 * ocorrência e mostra o estado atual.
 */
export async function repetirEmConflito<T>(chamada: () => Promise<T>): Promise<T> {
  for (let tentativa = 0; ; tentativa += 1) {
    try {
      return await chamada();
    } catch (e) {
      const erro = converterErro(e);
      if (erro.tipo !== 'conflito' || tentativa >= TENTATIVAS_EXTRAS) {
        throw erro;
      }
      await new Promise((resolver) => setTimeout(resolver, PAUSA_MS));
    }
  }
}
