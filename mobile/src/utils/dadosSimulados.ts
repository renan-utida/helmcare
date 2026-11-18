import type { ContatoRequisicao, CriarEntregadorRequisicao } from '@/tipos/contrato';

/**
 * Dados SIMULADOS para o cadastro. O contrato (seção 2) exige que todo dado pessoal do
 * HelmCare seja simulado, nunca de pessoas reais. Os nomes são combinações aleatórias
 * de listas comuns e os números são sorteados com os tamanhos que a API exige.
 */

const PRIMEIROS_NOMES = [
  'Ana', 'Bruno', 'Carla', 'Diego', 'Elisa', 'Felipe', 'Gabriela', 'Hugo', 'Isabela', 'João',
  'Karina', 'Lucas', 'Marina', 'Nicolas', 'Olívia', 'Paulo', 'Rafaela', 'Samuel', 'Tatiane', 'Vinícius',
];

const SOBRENOMES = [
  'Almeida', 'Barbosa', 'Cardoso', 'Duarte', 'Esteves', 'Ferreira', 'Gomes', 'Haddad', 'Iwamoto', 'Jardim',
  'Lima', 'Moraes', 'Nogueira', 'Oliveira', 'Pereira', 'Queiroz', 'Ribeiro', 'Santos', 'Teixeira', 'Vieira',
];

/** Parentescos oferecidos em botões no formulário do contato. */
export const PARENTESCOS = ['Mãe', 'Pai', 'Cônjuge', 'Irmão ou irmã', 'Amigo ou amiga', 'Outro'] as const;

/** Um dos parentescos oferecidos em botões. */
export type Parentesco = (typeof PARENTESCOS)[number];

/** Inteiro aleatório de 0 até `maximo - 1`. */
function sortear(maximo: number): number {
  return Math.floor(Math.random() * maximo);
}

/** Escolhe um item aleatório da lista. */
function escolher<T>(lista: readonly T[]): T {
  // A lista nunca é vazia aqui; o `as` só diz isso ao TypeScript.
  return lista[sortear(lista.length)] as T;
}

/** Sequência de `tamanho` dígitos aleatórios. */
function digitos(tamanho: number): number[] {
  return Array.from({ length: tamanho }, () => sortear(10));
}

/** Nome completo simulado (nome e dois sobrenomes diferentes). */
function nomeSimulado(): string {
  const primeiro = escolher(SOBRENOMES);
  let segundo = escolher(SOBRENOMES);
  while (segundo === primeiro) {
    segundo = escolher(SOBRENOMES);
  }
  return `${escolher(PRIMEIROS_NOMES)} ${primeiro} ${segundo}`;
}

/**
 * CPF simulado com máscara ("123.456.789-01"). O último dígito é trocado de propósito para
 * ficar errado: assim o número nunca é um CPF válido de alguém de verdade.
 * A API só confere os 11 dígitos, então ele é aceito.
 */
function cpfSimulado(): string {
  const base = digitos(9);
  const dv = (numeros: number[]) => {
    const soma = numeros.reduce((total, n, i) => total + n * (numeros.length + 1 - i), 0);
    const resto = (soma * 10) % 11;
    return resto === 10 ? 0 : resto;
  };
  const dv1 = dv(base);
  const dvCerto = dv([...base, dv1]);
  const dvErrado = (dvCerto + 1 + sortear(9)) % 10; // Sempre diferente do certo.
  const n = [...base, dv1, dvErrado].join('');
  return `${n.slice(0, 3)}.${n.slice(3, 6)}.${n.slice(6, 9)}-${n.slice(9)}`;
}

/** CNH simulada: 11 dígitos aleatórios. */
function cnhSimulada(): string {
  return digitos(11).join('');
}

/** Celular simulado de São Paulo com máscara: "(11) 9XXXX-XXXX". */
function celularSimulado(): string {
  const n = digitos(8).join('');
  return `(11) 9${n.slice(0, 4)}-${n.slice(4)}`;
}

/**
 * Gera os dados pessoais simulados do entregador para o `POST /entregadores`. O e-mail e a senha
 * ficam de fora: a pessoa digita os dela, porque vai usá-los para entrar.
 */
export function gerarEntregadorSimulado(): Omit<CriarEntregadorRequisicao, 'email' | 'senha'> {
  return {
    nome: nomeSimulado(),
    cpf: cpfSimulado(),
    cnh: cnhSimulada(),
    telefone: celularSimulado(),
  };
}

/** Gera os dados simulados do contato de emergência para o `POST /entregadores/{id}/contatos`. */
export function gerarContatoSimulado(): ContatoRequisicao & { parentesco: Parentesco } {
  return {
    nome: nomeSimulado(),
    telefone: celularSimulado(),
    parentesco: escolher(PARENTESCOS),
  };
}
