import { isAxiosError } from 'axios';

import type { ProblemDetails } from '@/tipos/contrato';

/**
 * Tipo do erro, decidido só pelo status HTTP (seção 7 do contrato):
 * 400 validação, 401 sem login válido, 403 sem permissão, 404 não encontrado,
 * 409 conflito, 503 indisponível, sem resposta (rede ou tempo esgotado) e qualquer outro caso.
 */
export type TipoErroApi =
  | 'validacao'
  | 'nao-autorizado'
  | 'proibido'
  | 'nao-encontrado'
  | 'conflito'
  | 'indisponivel'
  | 'sem-conexao'
  | 'desconhecido';

/**
 * Erro da API já traduzido para o app. Cada tela escolhe a própria mensagem
 * pelo `tipo`; o texto do `title` e do `detail` da API nunca é usado.
 */
export class ErroApi extends Error {
  /** Tipo do erro, pelo status HTTP. */
  readonly tipo: TipoErroApi;
  /** Status HTTP, ou `null` quando não houve resposta. */
  readonly status: number | null;
  /** Nomes dos campos inválidos de um 400 (chaves do `errors`), para a tela marcar. */
  readonly campos: string[];

  constructor(tipo: TipoErroApi, status: number | null, campos: string[] = []) {
    super(`ErroApi ${tipo}${status !== null ? ` (${status})` : ''}`);
    this.name = 'ErroApi';
    this.tipo = tipo;
    this.status = status;
    this.campos = campos;
  }
}

/** Converte o status HTTP no tipo do erro. */
function tipoPeloStatus(status: number): TipoErroApi {
  switch (status) {
    case 400:
      return 'validacao';
    case 401:
      return 'nao-autorizado';
    case 403:
      return 'proibido';
    case 404:
      return 'nao-encontrado';
    case 409:
      return 'conflito';
    case 503:
      return 'indisponivel';
    default:
      return 'desconhecido';
  }
}

/**
 * Lê as chaves do `errors` do ProblemDetails. A API usa o nome do campo em camelCase
 * ("cpf") e, quando o JSON não pôde ser lido, o caminho JSON ("$.parentesco"),
 * então o prefixo "$." é retirado. O texto das mensagens é ignorado de propósito.
 */
function camposInvalidos(corpo: unknown): string[] {
  if (typeof corpo !== 'object' || corpo === null || !('errors' in corpo)) {
    return [];
  }
  const { errors } = corpo as ProblemDetails; // Já sabemos que é objeto com `errors`.
  if (typeof errors !== 'object' || errors === null) {
    return [];
  }
  return Object.keys(errors).map((chave) => chave.replace(/^\$\./, ''));
}

/** Converte qualquer erro (do Axios ou não) em `ErroApi`. */
export function converterErro(erro: unknown): ErroApi {
  if (erro instanceof ErroApi) {
    return erro;
  }
  if (isAxiosError(erro)) {
    // Sem `response`: a requisição não chegou à API (sem rede, IP errado ou tempo esgotado).
    if (!erro.response) {
      return new ErroApi('sem-conexao', null);
    }
    const { status, data } = erro.response;
    const tipo = tipoPeloStatus(status);
    return new ErroApi(tipo, status, tipo === 'validacao' ? camposInvalidos(data) : []);
  }
  return new ErroApi('desconhecido', null);
}
