import type { ErroApi } from '@/servicos/erros';

/** Mensagem para quando o celular não alcança a API (sem rede, IP errado ou tempo esgotado). */
export const MENSAGEM_SEM_CONEXAO =
  'Sem conexão com a API. Confira se o celular e o computador estão na mesma rede, ou ajuste o endereço em Configurações.';

/** Mensagem para erros sem tratamento próprio na tela. */
export const MENSAGEM_ERRO_GENERICO = 'Algo deu errado. Tente de novo em alguns segundos.';

/** Mensagem para um 400 em que os campos marcados mostram o que corrigir. */
export const MENSAGEM_CONFIRA_CAMPOS = 'Confira os campos marcados.';

/**
 * Mensagem dos erros que todas as telas tratam igual (sem conexão e desconhecido).
 * Os demais tipos (400, 404, 409, 503) cada tela decide, pelo status, com o próprio texto.
 */
export function mensagemComum(erro: ErroApi): string {
  return erro.tipo === 'sem-conexao' ? MENSAGEM_SEM_CONEXAO : MENSAGEM_ERRO_GENERICO;
}

/**
 * Monta os erros de um formulário a partir das chaves do 400 (nomes dos campos do JSON).
 * Só marca os campos que a tela conhece; o texto da API não é usado.
 */
export function errosDosCampos<C extends string>(
  campos: string[],
  conhecidos: readonly C[],
  mensagem = 'Confira este campo.',
): Partial<Record<C, string>> {
  const erros: Partial<Record<C, string>> = {};
  for (const campo of conhecidos) {
    if (campos.includes(campo)) {
      erros[campo] = mensagem;
    }
  }
  return erros;
}
