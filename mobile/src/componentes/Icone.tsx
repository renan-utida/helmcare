import Ionicons from '@expo/vector-icons/Ionicons';
import type { ComponentProps } from 'react';

/** Nome de um ícone do conjunto Ionicons. */
export type NomeIcone = ComponentProps<typeof Ionicons>['name'];

/** Cor aceita pelo ícone (texto do tema ou a cor que a barra de abas entrega). */
export type CorIcone = NonNullable<ComponentProps<typeof Ionicons>['color']>;

/** Props do ícone decorativo. */
interface IconeProps {
  nome: NomeIcone;
  cor: CorIcone;
  tamanho?: number;
}

/**
 * Ícone decorativo: fica fora do leitor de tela, porque o texto ao lado
 * sempre diz a mesma coisa (a cor e o ícone nunca são a única informação).
 */
export function Icone({ nome, cor, tamanho = 24 }: IconeProps) {
  return (
    <Ionicons
      name={nome}
      color={cor}
      size={tamanho}
      accessible={false}
      importantForAccessibility="no-hide-descendants"
      accessibilityElementsHidden
    />
  );
}
