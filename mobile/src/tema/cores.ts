import type { FaixaRisco } from '@/tipos/contrato';

/**
 * Paletas clara e escura com as cores da marca HelmCare:
 * azul-marinho #001D48 (principal) e laranja #FE7801 (destaque).
 *
 * Regras de contraste (WCAG 2.x), conferidas por script em todos os pares texto e fundo:
 * - todo texto fica em 4,5:1 ou mais sobre o fundo em que aparece;
 * - ícones, bordas e barras que transmitem informação ficam em 3:1 ou mais;
 * - no tema claro, o laranja #FE7801 (2,7:1 sobre branco) nunca é texto: só ícone de
 *   enfeite, borda e faixa decorativa. Texto laranja usa #B45300 (5,0:1);
 * - no tema escuro, o fundo é azul-marinho e o #FE7801 pode ser texto (6,2:1);
 * - botão laranja leva texto azul-marinho, nunca branco.
 * As faixas de risco têm cores próprias, separadas da marca, e nenhuma usa laranja.
 */

/** Azul-marinho da marca. */
export const AZUL_MARINHO = '#001D48';
/** Laranja da marca. */
export const LARANJA = '#FE7801';
/** Laranja escuro para texto laranja no tema claro. */
export const LARANJA_ESCURO = '#B45300';
/** Branco. */
const BRANCO = '#FFFFFF';

/** Cores de um tipo de aviso: fundo, texto e ícone. */
export interface CoresAviso {
  fundo: string;
  texto: string;
  icone: string;
}

/** Cores do selo de uma faixa de risco. `borda` só existe no tema escuro. */
export interface CoresFaixa {
  fundo: string;
  texto: string;
  borda: string | null;
}

/** Todas as cores que as telas podem usar (sempre pelo `useTema()`). */
export interface Cores {
  /** Fundo das telas. */
  fundo: string;
  /** Fundo de cartões, campos, cabeçalho e barra de abas. */
  superficie: string;
  /** Texto principal. */
  texto: string;
  /** Texto de apoio (legendas, dicas, exemplo dentro do campo). */
  textoSecundario: string;
  /** Cor da ação principal: fundo do botão principal, texto e borda do secundário, barras. */
  primaria: string;
  /** Texto sobre a cor principal. */
  textoSobrePrimaria: string;
  /** Laranja da marca. No tema claro, só enfeite (nunca texto nem informação). */
  destaque: string;
  /** Laranja que pode ser usado em texto, nos dois temas. */
  textoDestaque: string;
  /** Ações perigosas (encerrar, cancelar). */
  perigo: string;
  /** Texto sobre a cor de perigo. */
  textoSobrePerigo: string;
  /** Texto de erro dos campos. */
  erro: string;
  /** Borda de campos (3:1 ou mais). */
  borda: string;
  /** Trilha das barras do Índice. */
  trilha: string;
  /** Texto e ícone da aba selecionada. */
  abaAtiva: string;
  /** Texto e ícone das abas não selecionadas. */
  abaInativa: string;
  /**
   * Fundo arredondado atrás do símbolo azul-marinho no cabeçalho, ou `null` quando o
   * símbolo já aparece bem sobre a superfície (tema claro).
   */
  fundoSimbolo: string | null;
  /** Avisos de informação, sucesso, atenção e erro. */
  aviso: Record<'informacao' | 'sucesso' | 'atencao' | 'erro', CoresAviso>;
  /** Selos das faixas de risco. */
  faixa: Record<FaixaRisco, CoresFaixa>;
}

/** Faixas de risco, iguais nos dois temas (o escuro só acrescenta a borda clara). */
const FAIXAS: Record<FaixaRisco, Omit<CoresFaixa, 'borda'>> = {
  BAIXO: { fundo: '#1B7F3B', texto: BRANCO },
  MODERADO: { fundo: '#F2C12E', texto: AZUL_MARINHO },
  ALTO: { fundo: '#C62828', texto: BRANCO },
  MUITO_ALTO: { fundo: '#6A1B4D', texto: BRANCO },
};

/** Monta as faixas com ou sem a borda clara de 2 dp. */
function faixas(borda: string | null): Record<FaixaRisco, CoresFaixa> {
  return {
    BAIXO: { ...FAIXAS.BAIXO, borda },
    MODERADO: { ...FAIXAS.MODERADO, borda },
    ALTO: { ...FAIXAS.ALTO, borda },
    MUITO_ALTO: { ...FAIXAS.MUITO_ALTO, borda },
  };
}

/** Paleta do tema claro: fundo branco, texto e botão principal azul-marinho. */
export const coresClaras: Cores = {
  fundo: BRANCO,
  superficie: '#F2F5FA',
  texto: AZUL_MARINHO,
  textoSecundario: '#44526B',
  primaria: AZUL_MARINHO,
  textoSobrePrimaria: BRANCO,
  destaque: LARANJA,
  textoDestaque: LARANJA_ESCURO,
  perigo: '#B3261E',
  textoSobrePerigo: BRANCO,
  erro: '#B3261E',
  borda: '#5F6E86',
  trilha: '#D6DDE8',
  abaAtiva: AZUL_MARINHO,
  abaInativa: '#44526B',
  fundoSimbolo: null,
  aviso: {
    informacao: { fundo: '#E5EDF8', texto: AZUL_MARINHO, icone: AZUL_MARINHO },
    sucesso: { fundo: '#E3F3E8', texto: AZUL_MARINHO, icone: '#1B6E3A' },
    atencao: { fundo: '#FFF1E3', texto: AZUL_MARINHO, icone: LARANJA_ESCURO },
    erro: { fundo: '#FCE8E6', texto: AZUL_MARINHO, icone: '#B3261E' },
  },
  faixa: faixas(null),
};

/** Paleta do tema escuro: fundo azul-marinho, texto branco e botão principal laranja. */
export const coresEscuras: Cores = {
  fundo: AZUL_MARINHO,
  superficie: '#0B2C5E',
  texto: BRANCO,
  textoSecundario: '#C5D0E2',
  primaria: LARANJA,
  textoSobrePrimaria: AZUL_MARINHO,
  destaque: LARANJA,
  textoDestaque: LARANJA,
  perigo: '#FF8A80',
  textoSobrePerigo: AZUL_MARINHO,
  erro: '#FFB4AB',
  borda: '#8FA3C4',
  trilha: '#2B4A7D',
  abaAtiva: LARANJA,
  abaInativa: '#C5D0E2',
  // O símbolo azul-marinho fica com 1,21:1 sobre a superfície escura: precisa do fundo branco.
  fundoSimbolo: BRANCO,
  aviso: {
    informacao: { fundo: '#0E3570', texto: BRANCO, icone: '#9CC3FF' },
    sucesso: { fundo: '#0D3B2E', texto: BRANCO, icone: '#7FD49C' },
    atencao: { fundo: '#3F2A12', texto: BRANCO, icone: LARANJA },
    erro: { fundo: '#4A1A2A', texto: BRANCO, icone: '#FFB4AB' },
  },
  // O verde e o vinho das faixas têm pouco contraste com o azul-marinho: borda clara de 2 dp.
  faixa: faixas(BRANCO),
};
