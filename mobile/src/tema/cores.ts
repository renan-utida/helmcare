import type { FaixaRisco } from '@/tipos/contrato';

/**
 * Paletas clara e escura. Contraste conferido com a fórmula da WCAG 2.x:
 * todo texto fica em 4,5:1 ou mais sobre o fundo em que aparece, e bordas,
 * ícones e barras ficam em 3:1 ou mais (elementos que não são texto).
 * As razões de cada par estão nos comentários (claro / escuro).
 */

/** Cores de um tipo de aviso: fundo, texto e ícone. */
export interface CoresAviso {
  fundo: string;
  texto: string;
  icone: string;
}

/** Cores do selo de uma faixa de risco: fundo e texto legível sobre ele. */
export interface CoresFaixa {
  fundo: string;
  texto: string;
}

/** Todas as cores que as telas podem usar (sempre pelo `useTema()`). */
export interface Cores {
  /** Fundo das telas. */
  fundo: string;
  /** Fundo de cartões, campos e barra de abas. */
  superficie: string;
  /** Texto principal: 17,74 / 17,17 sobre o fundo; 16,08 / 14,82 sobre a superfície. */
  texto: string;
  /** Texto de apoio: 7,56 / 11,25 sobre o fundo; 6,85 / 9,71 sobre a superfície. */
  textoSecundario: string;
  /** Ação principal e links: 7,21 / 8,97 sobre o fundo. */
  primaria: string;
  /** Texto sobre a cor primária: 7,21 / 8,97. */
  textoSobrePrimaria: string;
  /** Ações perigosas (encerrar, cancelar). */
  perigo: string;
  /** Texto sobre a cor de perigo: 6,57 / 9,43. */
  textoSobrePerigo: string;
  /** Texto de erro dos campos: 6,57 / 9,43 sobre o fundo. */
  erro: string;
  /** Borda de campos e botões secundários: 4,83 / 5,96 (mínimo de 3:1 para não texto). */
  borda: string;
  /** Trilha das barras: a barra primária fica em 5,17 / 5,33 sobre ela. */
  trilha: string;
  /** Avisos: texto de 15,06 a 15,87 / 12,58 a 14,34; ícone de 5,58 a 6,15 / 6,90 a 8,57. */
  aviso: Record<'informacao' | 'sucesso' | 'atencao' | 'erro', CoresAviso>;
  /** Selos das faixas: claro de 5,43 a 10,59; escuro de 9,43 a 12,70. */
  faixa: Record<FaixaRisco, CoresFaixa>;
}

/** Paleta do tema claro. */
export const coresClaras: Cores = {
  fundo: '#FFFFFF',
  superficie: '#F1F4F8',
  texto: '#111827',
  textoSecundario: '#4B5563',
  primaria: '#0B57A4',
  textoSobrePrimaria: '#FFFFFF',
  perigo: '#B42318',
  textoSobrePerigo: '#FFFFFF',
  erro: '#B42318',
  borda: '#6B7280',
  trilha: '#D5DBE3',
  aviso: {
    informacao: { fundo: '#E5EEF9', texto: '#111827', icone: '#0B57A4' },
    sucesso: { fundo: '#E3F3E8', texto: '#111827', icone: '#1A6B37' },
    atencao: { fundo: '#FDF1DC', texto: '#111827', icone: '#8A4B00' },
    erro: { fundo: '#FCE8E6', texto: '#111827', icone: '#B42318' },
  },
  faixa: {
    BAIXO: { fundo: '#1A6B37', texto: '#FFFFFF' }, // 6,56
    MODERADO: { fundo: '#F2C230', texto: '#111827' }, // 10,59
    ALTO: { fundo: '#B54708', texto: '#FFFFFF' }, // 5,43
    MUITO_ALTO: { fundo: '#912018', texto: '#FFFFFF' }, // 8,66
  },
};

/** Paleta do tema escuro. */
export const coresEscuras: Cores = {
  fundo: '#0F1115',
  superficie: '#1C2029',
  texto: '#F3F4F6',
  textoSecundario: '#C3C8D1',
  primaria: '#8AB4F8',
  textoSobrePrimaria: '#0F1115',
  perigo: '#F4A29A',
  textoSobrePerigo: '#0F1115',
  erro: '#F4A29A',
  borda: '#8A919E',
  trilha: '#353B47',
  aviso: {
    informacao: { fundo: '#132A45', texto: '#F3F4F6', icone: '#8AB4F8' },
    sucesso: { fundo: '#12301D', texto: '#F3F4F6', icone: '#7FD49C' },
    atencao: { fundo: '#3A2A0C', texto: '#F3F4F6', icone: '#F5C46B' },
    erro: { fundo: '#3D1714', texto: '#F3F4F6', icone: '#F4A29A' },
  },
  faixa: {
    BAIXO: { fundo: '#7FD49C', texto: '#0F1115' }, // 10,62
    MODERADO: { fundo: '#F5D06B', texto: '#0F1115' }, // 12,70
    ALTO: { fundo: '#F9A86B', texto: '#0F1115' }, // 9,76
    MUITO_ALTO: { fundo: '#F4A29A', texto: '#0F1115' }, // 9,43
  },
};
