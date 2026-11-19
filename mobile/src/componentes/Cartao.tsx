import type { ReactNode } from 'react';
import { View, type ViewProps } from 'react-native';

import { useTema } from '@/contextos/TemaContext';

import { Texto } from './Texto';

/** Props do `Cartao`. */
export interface CartaoProps extends ViewProps {
  /** Título opcional, lido como cabeçalho. */
  titulo?: string;
  /** Ação no canto superior direito, ao lado do título (por exemplo, o botão de olho). */
  acao?: ReactNode;
  children?: ReactNode;
}

/** Bloco com fundo de superfície, cantos arredondados e espaçamento interno, para agrupar informações. */
export function Cartao({ titulo, acao, children, style, ...resto }: CartaoProps) {
  const { cores, espaco, raio } = useTema();
  return (
    <View
      {...resto}
      style={[
        {
          // Visual limpo: fundo de superfície e cantos de 16, sem contorno. O cartão se separa do
          // fundo pela cor (a superfície) e pelo espaço entre os blocos.
          backgroundColor: cores.superficie,
          borderRadius: raio.medio,
          padding: espaco.medio,
          gap: espaco.medio,
        },
        style,
      ]}
    >
      {titulo || acao ? (
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: espaco.pequeno }}>
          {titulo ? (
            <Texto variante="subtitulo" style={{ flex: 1 }}>
              {titulo}
            </Texto>
          ) : (
            <View style={{ flex: 1 }} />
          )}
          {acao}
        </View>
      ) : null}
      {children}
    </View>
  );
}
