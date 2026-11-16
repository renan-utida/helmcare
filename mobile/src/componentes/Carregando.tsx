import { ActivityIndicator, View } from 'react-native';

import { useTema } from '@/contextos/TemaContext';

import { LogoHorizontal } from './Marca';
import { Texto } from './Texto';

/** Props do `Carregando`. */
export interface CarregandoProps {
  /** Texto mostrado e anunciado (padrão: "Carregando..."). */
  mensagem?: string;
  /**
   * Mostra o logo do HelmCare acima do texto. Usado na abertura do app: o Expo Go
   * não mostra o splash do app, então o logo aparece aqui logo na primeira abertura.
   */
  comLogo?: boolean;
}

/** Indicador de carregamento centralizado, com texto visível e anunciado ao leitor de tela. */
export function Carregando({ mensagem = 'Carregando...', comLogo = false }: CarregandoProps) {
  const { cores, espaco } = useTema();
  return (
    <View
      accessible
      accessibilityRole="progressbar"
      accessibilityLabel={comLogo ? `HelmCare. ${mensagem}` : mensagem}
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
      {comLogo ? (
        <View style={{ marginBottom: espaco.medio }}>
          <LogoHorizontal />
        </View>
      ) : null}
      <ActivityIndicator size="large" color={cores.primaria} />
      <Texto centralizado>{mensagem}</Texto>
    </View>
  );
}
