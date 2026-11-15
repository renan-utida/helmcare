import type { ReactNode } from 'react';
import { View, type ViewProps } from 'react-native';

import { useTema } from '@/contextos/TemaContext';

import { Texto } from './Texto';

/** Props do `Cartao`. */
export interface CartaoProps extends ViewProps {
  /** Título opcional, lido como cabeçalho. */
  titulo?: string;
  children?: ReactNode;
}

/** Bloco com fundo de superfície, borda e espaçamento interno, para agrupar informações. */
export function Cartao({ titulo, children, style, ...resto }: CartaoProps) {
  const { cores, espaco, raio } = useTema();
  return (
    <View
      {...resto}
      style={[
        {
          backgroundColor: cores.superficie,
          borderColor: cores.trilha,
          borderWidth: 1,
          borderRadius: raio.medio,
          padding: espaco.medio,
          gap: espaco.medio,
        },
        style,
      ]}
    >
      {titulo ? <Texto variante="subtitulo">{titulo}</Texto> : null}
      {children}
    </View>
  );
}
