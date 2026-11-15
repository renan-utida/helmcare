import { Text, type TextProps } from 'react-native';

import { useTema } from '@/contextos/TemaContext';
import type { VarianteTexto } from '@/tema';

/** Cores do tema que podem ser usadas direto num texto. */
type CorTexto = 'texto' | 'textoSecundario' | 'primaria' | 'erro' | 'textoSobrePrimaria' | 'textoSobrePerigo';

/** Props do `Texto`: as do `Text` mais a variante e a cor do tema. */
export interface TextoProps extends TextProps {
  /** Variante da tipografia (padrão: corpo). */
  variante?: VarianteTexto;
  /** Cor do tema (padrão: texto; legenda usa textoSecundario). */
  cor?: CorTexto;
  /** Outra cor do tema (ex.: texto de um aviso ou de um selo). Tem prioridade sobre `cor`. */
  corPersonalizada?: string;
  /** Centraliza o texto. */
  centralizado?: boolean;
}

/**
 * Texto com a tipografia e as cores do tema. O tamanho escala com a fonte do sistema.
 * Títulos e subtítulos viram cabeçalho para o leitor de tela navegar por eles.
 */
export function Texto({ variante = 'corpo', cor, corPersonalizada, centralizado, style, ...resto }: TextoProps) {
  const { cores, tipografia } = useTema();
  const corPadrao: CorTexto = variante === 'legenda' ? 'textoSecundario' : 'texto';
  const ehCabecalho = variante === 'titulo' || variante === 'subtitulo';
  return (
    <Text
      accessibilityRole={ehCabecalho ? 'header' : undefined}
      {...resto}
      style={[
        tipografia[variante],
        { color: corPersonalizada ?? cores[cor ?? corPadrao] },
        centralizado ? { textAlign: 'center' } : null,
        style,
      ]}
    />
  );
}
