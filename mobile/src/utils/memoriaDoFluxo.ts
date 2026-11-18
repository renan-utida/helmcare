/**
 * Passagem de dados entre duas telas SÓ em memória, para o que não pode ir na URL da rota nem no
 * histórico de navegação (parâmetros de rota ficam guardados pelo Expo Router e aparecem em log
 * de navegação). Cada valor é lido uma vez e apagado na leitura, e tudo some ao fechar o app.
 *
 * Usos: o e-mail que preenche o login depois de um cadastro cujo login automático falhou.
 */

/** O que cada chave leva. */
interface ValoresDoFluxo {
  /** E-mail digitado no cadastro, para preencher o login (dado pessoal: nunca na rota). */
  emailLogin: string;
}

/** Chave de um valor do fluxo. */
type ChaveFluxo = keyof ValoresDoFluxo;

/** Valores guardados agora, por chave. */
const valores = new Map<ChaveFluxo, ValoresDoFluxo[ChaveFluxo]>();

/** Guarda um valor para a próxima tela ler. */
export function guardarParaProximaTela<C extends ChaveFluxo>(chave: C, valor: ValoresDoFluxo[C]): void {
  valores.set(chave, valor);
}

/** Lê e apaga o valor da chave, ou `null` quando não há. */
export function retirarDoFluxo<C extends ChaveFluxo>(chave: C): ValoresDoFluxo[C] | null {
  const valor = valores.get(chave);
  valores.delete(chave);
  // O Map guarda os valores de todas as chaves juntos; quem gravou nesta chave foi o
  // `guardarParaProximaTela` com o mesmo tipo, então o valor é desta chave.
  return (valor as ValoresDoFluxo[C] | undefined) ?? null;
}
