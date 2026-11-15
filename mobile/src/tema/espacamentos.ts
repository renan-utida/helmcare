/** Espaçamentos em múltiplos de 8 (dp). */
export const espaco = {
  pequeno: 8,
  medio: 16,
  grande: 24,
  extra: 32,
  enorme: 48,
} as const;

/** Raios de borda. */
export const raio = {
  pequeno: 8,
  medio: 16,
  /** Raio que deixa o selo em formato de pílula. */
  pilula: 999,
} as const;

/** Medidas mínimas de acessibilidade. */
export const medidas = {
  /** Área de toque mínima de qualquer elemento clicável (48 x 48 dp). */
  toqueMinimo: 48,
  /** Altura mínima dos botões principais. */
  alturaBotao: 56,
  /** Tamanho padrão dos ícones. */
  icone: 24,
} as const;
