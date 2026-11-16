import { TIPOS_SANGUINEOS, type TipoSanguineo } from '@/tipos/contrato';

/**
 * Validações do app, com as mesmas regras da seção 3 do contrato (a API rejeita com 400).
 * Cada função devolve a mensagem de erro em português simples, ou `null` quando está tudo certo.
 * CPF, CNH e telefone aceitam máscara: só os dígitos contam.
 */

/** Tamanho máximo do nome (seção 3). */
export const TAMANHO_MAXIMO_NOME = 120;
/** Tamanho máximo do parentesco aceito pela API. */
export const TAMANHO_MAXIMO_PARENTESCO = 40;
/** Tamanho máximo de alergias, condições críticas e medicamentos. */
export const TAMANHO_MAXIMO_TEXTO_FICHA = 500;

/** Tira tudo que não é dígito ("123.456.789-01" vira "12345678901"). */
export function somenteDigitos(texto: string): string {
  return texto.replace(/\D/g, '');
}

/** Nome: obrigatório, até 120 caracteres. */
export function validarNome(nome: string): string | null {
  const limpo = nome.trim();
  if (limpo === '') {
    return 'Escreva o nome.';
  }
  if (limpo.length > TAMANHO_MAXIMO_NOME) {
    return `Use até ${TAMANHO_MAXIMO_NOME} letras.`;
  }
  return null;
}

/** CPF: 11 dígitos, com ou sem máscara. */
export function validarCpf(cpf: string): string | null {
  return somenteDigitos(cpf).length === 11 ? null : 'O CPF precisa ter 11 números.';
}

/** CNH: 11 dígitos, com ou sem máscara. */
export function validarCnh(cnh: string): string | null {
  return somenteDigitos(cnh).length === 11 ? null : 'A CNH precisa ter 11 números.';
}

/** Telefone: 10 ou 11 dígitos, com DDD. */
export function validarTelefone(telefone: string): string | null {
  const tamanho = somenteDigitos(telefone).length;
  return tamanho === 10 || tamanho === 11 ? null : 'O telefone precisa ter DDD e 10 ou 11 números.';
}

/** Parentesco do contato: obrigatório, até 40 caracteres. */
export function validarParentesco(parentesco: string): string | null {
  const limpo = parentesco.trim();
  if (limpo === '') {
    return 'Escolha o parentesco.';
  }
  return limpo.length > TAMANHO_MAXIMO_PARENTESCO ? `Use até ${TAMANHO_MAXIMO_PARENTESCO} letras.` : null;
}

/** Confere se o texto é um dos 8 tipos sanguíneos aceitos (em maiúsculas). */
export function ehTipoSanguineo(valor: string): valor is TipoSanguineo {
  return (TIPOS_SANGUINEOS as readonly string[]).includes(valor);
}

/** Tipo sanguíneo: um dos 8 da lista. */
export function validarTipoSanguineo(valor: string | null): string | null {
  return valor !== null && ehTipoSanguineo(valor) ? null : 'Escolha o tipo sanguíneo.';
}

/** Alergias, condições críticas e medicamentos: opcionais, até 500 caracteres. */
export function validarTextoFicha(texto: string): string | null {
  return texto.trim().length > TAMANHO_MAXIMO_TEXTO_FICHA ? `Use até ${TAMANHO_MAXIMO_TEXTO_FICHA} letras.` : null;
}

/** Deixa o código do capacete no formato da API (maiúsculas, sem espaços). */
export function normalizarCodigoCapacete(codigo: string): string {
  return codigo.trim().toUpperCase();
}

/** Código do capacete: formato HC-0000 (ex.: HC-0001). */
export function validarCodigoCapacete(codigo: string): string | null {
  return /^HC-\d{4}$/.test(normalizarCodigoCapacete(codigo)) ? null : 'Use o formato HC-0000, como HC-0001.';
}

/** Id do entregador: um GUID (ex.: 3f2a9c1e-5b7d-4e8a-9c21-7d4e5f6a8b90). */
export function validarGuid(id: string): string | null {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id.trim())
    ? null
    : 'O código do cadastro tem 36 caracteres, com letras, números e tracinhos.';
}

/** URL manual da API: começa com http:// ou https:// e tem um endereço depois. */
export function validarUrlApi(url: string): string | null {
  return /^https?:\/\/[^\s/]+/i.test(url.trim())
    ? null
    : 'O endereço começa com http:// e tem o IP do computador, como http://192.168.0.10:5000/api/v1';
}
