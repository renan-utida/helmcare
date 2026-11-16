import type { TextStyle } from 'react-native';

/**
 * Tamanhos de fonte em pontos de base. O Android multiplica cada um pela escala de
 * fonte do sistema, então nenhum texto usa `allowFontScaling={false}` nem altura fixa.
 * Também não há `lineHeight` fixo, para a linha crescer junto com a fonte.
 */
export const tipografia = {
  /** Número grande do Índice de Risco. */
  destaque: { fontSize: 48, fontWeight: '700' },
  /** Título das telas. */
  titulo: { fontSize: 26, fontWeight: '700' },
  /** Subtítulo de seções e cartões. */
  subtitulo: { fontSize: 22, fontWeight: '700' },
  /** Texto do botão. */
  botao: { fontSize: 20, fontWeight: '700' },
  /** Texto corrido. */
  corpo: { fontSize: 18, fontWeight: '400' },
  /** Rótulos de campos e textos em negrito do corpo. */
  rotulo: { fontSize: 18, fontWeight: '700' },
  /** Textos de apoio (horários, dicas). Ainda grande, para leitura na rua. */
  legenda: { fontSize: 16, fontWeight: '400' },
} satisfies Record<string, TextStyle>;

/** Nome de uma variante de texto. */
export type VarianteTexto = keyof typeof tipografia;
