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
  /** Escurecido semitransparente atrás do painel lateral (enfeite: nenhum texto fica sobre ele). */
  sombraModal: string;
  /**
   * Módulos e fundo do QR Code do capacete: azul-marinho sobre branco nos DOIS temas (16:1), porque
   * os leitores de QR precisam de módulos escuros sobre fundo claro, inclusive no tema escuro.
   */
  qrModulo: string;
  qrFundo: string;
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
  sombraModal: 'rgba(0, 29, 72, 0.45)',
  qrModulo: AZUL_MARINHO,
  qrFundo: BRANCO,
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
  sombraModal: 'rgba(0, 0, 0, 0.6)',
  qrModulo: AZUL_MARINHO,
  qrFundo: BRANCO,
  aviso: {
    informacao: { fundo: '#0E3570', texto: BRANCO, icone: '#9CC3FF' },
    sucesso: { fundo: '#0D3B2E', texto: BRANCO, icone: '#7FD49C' },
    atencao: { fundo: '#3F2A12', texto: BRANCO, icone: LARANJA },
    erro: { fundo: '#4A1A2A', texto: BRANCO, icone: '#FFB4AB' },
  },
  // O verde e o vinho das faixas têm pouco contraste com o azul-marinho: borda clara de 2 dp.
  faixa: faixas(BRANCO),
};

// ---------------------------------------------------------------------------
// Paletas de cada modo (aba Ajustes, "Aparência"): HelmCare, Alto contraste e uma terceira
// (Areia no claro, Grafite no escuro), sempre com o azul-marinho e o laranja da marca.
// O teste de contraste confere as 6: texto em 4,5:1 (7:1 no Alto contraste), ícones e bordas em 3:1.
// ---------------------------------------------------------------------------

/**
 * Faixas do Alto contraste: o verde e o vermelho ficam mais escuros, para o texto branco do selo
 * passar de 7:1 (as outras duas já passam). O tom continua o mesmo, para a faixa ser reconhecida.
 */
function faixasAltoContraste(borda: string | null): Record<FaixaRisco, CoresFaixa> {
  return {
    BAIXO: { fundo: '#0E5526', texto: BRANCO, borda },
    MODERADO: { ...FAIXAS.MODERADO, borda },
    ALTO: { fundo: '#931A1A', texto: BRANCO, borda },
    MUITO_ALTO: { ...FAIXAS.MUITO_ALTO, borda },
  };
}

/** Paleta clara de alto contraste: preto-azulado no branco, mirando 7:1 em todo texto. */
export const coresClarasAltoContraste: Cores = {
  fundo: BRANCO,
  superficie: '#F2F4F8',
  texto: '#000F26',
  textoSecundario: '#1F2B40',
  primaria: AZUL_MARINHO,
  textoSobrePrimaria: BRANCO,
  destaque: LARANJA,
  textoDestaque: '#7A3800',
  perigo: '#8A1C16',
  textoSobrePerigo: BRANCO,
  erro: '#8A1C16',
  borda: '#2E3B52',
  trilha: '#C4CCD9',
  abaAtiva: AZUL_MARINHO,
  abaInativa: '#1F2B40',
  sombraModal: 'rgba(0, 15, 38, 0.55)',
  qrModulo: AZUL_MARINHO,
  qrFundo: BRANCO,
  aviso: {
    informacao: { fundo: '#E8EEF8', texto: '#000F26', icone: AZUL_MARINHO },
    sucesso: { fundo: '#E6F4EA', texto: '#000F26', icone: '#0F5129' },
    atencao: { fundo: '#FFF3E6', texto: '#000F26', icone: '#7A3800' },
    erro: { fundo: '#FCEBE9', texto: '#000F26', icone: '#8A1C16' },
  },
  faixa: faixasAltoContraste(null),
};

/** Paleta clara Areia: fundo de papel quente, texto azul-marinho e laranja só nos destaques. */
export const coresClarasAreia: Cores = {
  fundo: '#FBF6EE',
  superficie: '#F2E8D8',
  texto: AZUL_MARINHO,
  textoSecundario: '#4A4A4A',
  primaria: AZUL_MARINHO,
  textoSobrePrimaria: BRANCO,
  destaque: LARANJA,
  textoDestaque: '#9E4700',
  perigo: '#A3231B',
  textoSobrePerigo: BRANCO,
  erro: '#A3231B',
  borda: '#6E6556',
  trilha: '#DDD0BA',
  abaAtiva: AZUL_MARINHO,
  abaInativa: '#4A4A4A',
  sombraModal: 'rgba(40, 30, 10, 0.45)',
  qrModulo: AZUL_MARINHO,
  qrFundo: BRANCO,
  aviso: {
    informacao: { fundo: '#E7ECF3', texto: AZUL_MARINHO, icone: AZUL_MARINHO },
    sucesso: { fundo: '#E4F0E0', texto: AZUL_MARINHO, icone: '#1B6331' },
    atencao: { fundo: '#FCE9D2', texto: AZUL_MARINHO, icone: '#9E4700' },
    erro: { fundo: '#F8E2DC', texto: AZUL_MARINHO, icone: '#A3231B' },
  },
  faixa: faixas(null),
};

/** Paleta escura de alto contraste: azul-marinho mais fundo, texto branco e laranja forte. */
export const coresEscurasAltoContraste: Cores = {
  fundo: '#000C1F',
  superficie: '#001A40',
  texto: BRANCO,
  textoSecundario: '#E2E8F2',
  primaria: '#FF8A26',
  textoSobrePrimaria: '#000C1F',
  destaque: LARANJA,
  textoDestaque: '#FF9B45',
  perigo: '#FFA49C',
  textoSobrePerigo: '#000C1F',
  erro: '#FFC4BD',
  borda: '#B4C3DC',
  trilha: '#33507F',
  abaAtiva: '#FF9B45',
  abaInativa: '#E2E8F2',
  sombraModal: 'rgba(0, 0, 0, 0.7)',
  qrModulo: AZUL_MARINHO,
  qrFundo: BRANCO,
  aviso: {
    informacao: { fundo: '#082A5C', texto: BRANCO, icone: '#B8D4FF' },
    sucesso: { fundo: '#063325', texto: BRANCO, icone: '#9BE3B3' },
    atencao: { fundo: '#36230C', texto: BRANCO, icone: '#FF9B45' },
    erro: { fundo: '#40121F', texto: BRANCO, icone: '#FFC4BD' },
  },
  faixa: faixasAltoContraste(BRANCO),
};

/** Paleta escura Grafite: cinza-grafite neutro, texto claro e o laranja da marca nas ações. */
export const coresEscurasGrafite: Cores = {
  fundo: '#1A1D23',
  superficie: '#252A33',
  texto: '#F4F6F9',
  textoSecundario: '#C2C8D2',
  primaria: LARANJA,
  textoSobrePrimaria: AZUL_MARINHO,
  destaque: LARANJA,
  textoDestaque: LARANJA,
  perigo: '#FF8A80',
  textoSobrePerigo: '#1A1D23',
  erro: '#FFB4AB',
  borda: '#8D96A5',
  trilha: '#3A414D',
  abaAtiva: LARANJA,
  abaInativa: '#C2C8D2',
  sombraModal: 'rgba(0, 0, 0, 0.6)',
  qrModulo: AZUL_MARINHO,
  qrFundo: BRANCO,
  aviso: {
    informacao: { fundo: '#1F3050', texto: '#F4F6F9', icone: '#9CC3FF' },
    sucesso: { fundo: '#173626', texto: '#F4F6F9', icone: '#7FD49C' },
    atencao: { fundo: '#3D2C17', texto: '#F4F6F9', icone: LARANJA },
    erro: { fundo: '#46202A', texto: '#F4F6F9', icone: '#FFB4AB' },
  },
  faixa: faixas(BRANCO),
};

/** Paletas do modo claro. */
export type PaletaClara = 'helmcare' | 'alto-contraste' | 'areia';
/** Paletas do modo escuro. */
export type PaletaEscura = 'helmcare' | 'alto-contraste' | 'grafite';

/** As 3 paletas de cada modo, na ordem dos Ajustes, com o nome mostrado. */
export const PALETAS = {
  claro: [
    { id: 'helmcare', nome: 'HelmCare', cores: coresClaras },
    { id: 'alto-contraste', nome: 'Alto contraste', cores: coresClarasAltoContraste },
    { id: 'areia', nome: 'Areia', cores: coresClarasAreia },
  ],
  escuro: [
    { id: 'helmcare', nome: 'HelmCare', cores: coresEscuras },
    { id: 'alto-contraste', nome: 'Alto contraste', cores: coresEscurasAltoContraste },
    { id: 'grafite', nome: 'Grafite', cores: coresEscurasGrafite },
  ],
} as const satisfies {
  claro: readonly { id: PaletaClara; nome: string; cores: Cores }[];
  escuro: readonly { id: PaletaEscura; nome: string; cores: Cores }[];
};
