import { ActivityIndicator, View } from 'react-native';

import { useTema } from '@/contextos/TemaContext';

import { Texto } from './Texto';

/** Props do `Carregando`. */
export interface CarregandoProps {
  /** Texto mostrado e anunciado (padrão: "Carregando..."). */
  mensagem?: string;
}

/** Indicador de carregamento centralizado, com texto visível e anunciado ao leitor de tela. */
export function Carregando({ mensagem = 'Carregando...' }: CarregandoProps) {
  const { cores, espaco } = useTema();
  return (
    <View
      accessible
      accessibilityRole="progressbar"
      accessibilityLabel={mensagem}
      accessibilityLiveRegion="polite"
      style={{
        flex: 1,
        alignItems: 'center',
        justifyContent: 'center',
        gap: espaco.medio,
        padding: espaco.grande,
        backgroundColor: cores.fundo,
      }}
    >
      <ActivityIndicator size="large" color={cores.primaria} />
      <Texto centralizado>{mensagem}</Texto>
    </View>
  );
}
