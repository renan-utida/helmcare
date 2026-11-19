import { View } from 'react-native';

import { useTema } from '@/contextos/TemaContext';

import { Icone, type NomeIcone } from './Icone';

/** Tom do círculo: as mesmas famílias dos avisos (fundo levemente tingido e ícone forte). */
export type TomCirculo = 'informacao' | 'sucesso' | 'atencao' | 'erro';

/** Props do `IconeEmCirculo`. */
export interface IconeEmCirculoProps {
  nome: NomeIcone;
  tom?: TomCirculo;
  /** Diâmetro do círculo, em dp (padrão: 48). */
  tamanho?: number;
}

/**
 * Ícone dentro de um círculo com o fundo levemente tingido, como nos apps do dia a dia. É só
 * desenho (fora do leitor de tela): o texto ao lado sempre diz a mesma coisa. As cores vêm dos
 * pares de aviso do tema, já conferidos no teste de contraste (ícone em 3:1 ou mais sobre o fundo).
 */
export function IconeEmCirculo({ nome, tom = 'informacao', tamanho = 48 }: IconeEmCirculoProps) {
  const { cores } = useTema();
  const cor = cores.aviso[tom];
  return (
    <View
      accessible={false}
      importantForAccessibility="no-hide-descendants"
      style={{
        width: tamanho,
        height: tamanho,
        borderRadius: tamanho / 2,
        backgroundColor: cor.fundo,
        alignItems: 'center',
        justifyContent: 'center',
      }}
    >
      <Icone nome={nome} cor={cor.icone} tamanho={Math.round(tamanho * 0.55)} />
    </View>
  );
}
