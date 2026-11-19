import { Pressable } from 'react-native';

import { useTema } from '@/contextos/TemaContext';

import { Icone } from './Icone';

/** Props do `BotaoOlho`. */
export interface BotaoOlhoProps {
  /** Verdadeiro quando os dados estão à mostra. */
  visivel: boolean;
  aoAlternar: () => void;
  /** Rótulos para o leitor de tela (padrão: "Mostrar dados" e "Ocultar dados"). */
  rotuloMostrar?: string;
  rotuloOcultar?: string;
}

/**
 * Botão de olho (48 dp) que mostra ou esconde os dados de um cartão. O ícone troca entre o olho e
 * o olho cortado, e o leitor de tela diz a ação e o estado (expandido quando os dados aparecem).
 * Mostrar não desmascara nada: os dados aparecem como a API devolve, já mascarados.
 */
export function BotaoOlho({
  visivel,
  aoAlternar,
  rotuloMostrar = 'Mostrar dados',
  rotuloOcultar = 'Ocultar dados',
}: BotaoOlhoProps) {
  const { cores, medidas } = useTema();
  return (
    <Pressable
      onPress={aoAlternar}
      accessibilityRole="button"
      accessibilityLabel={visivel ? rotuloOcultar : rotuloMostrar}
      accessibilityState={{ expanded: visivel }}
      hitSlop={4}
      style={{ minWidth: medidas.toqueMinimo, minHeight: medidas.toqueMinimo, alignItems: 'center', justifyContent: 'center' }}
    >
      <Icone nome={visivel ? 'eye-off' : 'eye'} cor={cores.primaria} tamanho={26} />
    </Pressable>
  );
}
