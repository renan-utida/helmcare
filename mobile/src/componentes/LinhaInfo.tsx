import { View } from 'react-native';

import { useTema } from '@/contextos/TemaContext';

import { Texto } from './Texto';

/**
 * Transforma um valor mascarado pela API num texto bom de ouvir. Em vez de o leitor de tela
 * dizer "asterisco" várias vezes, diz, por exemplo, "DDD 11, parte oculta, termina em 1 2 3 4".
 * Não desmascara nada: usa só o que a API já mostrou.
 */
export function leituraMascarada(valor: string): string {
  if (!valor.includes('*')) {
    return valor;
  }
  const ddd = valor.match(/^\((\d{2})\)/)?.[1];
  const final = valor.match(/(\d+)\D*$/)?.[1];
  const partes = [
    ddd ? `DDD ${ddd}` : null,
    'parte oculta',
    final ? `termina em ${final.split('').join(' ')}` : null,
  ];
  return partes.filter((parte) => parte !== null).join(', ');
}

/** Props da `LinhaInfo`. */
export interface LinhaInfoProps {
  rotulo: string;
  valor: string;
  /** Verdadeiro quando o valor vem mascarado da API (CPF, CNH, telefone). */
  mascarado?: boolean;
  /** Permite copiar o valor com um toque longo (ex.: código do cadastro). */
  selecionavel?: boolean;
}

/** Par rótulo e valor, lido como uma frase só ("CPF: parte oculta, termina em 1 2"). */
export function LinhaInfo({ rotulo, valor, mascarado = false, selecionavel = false }: LinhaInfoProps) {
  const { espaco } = useTema();
  return (
    <View
      accessible
      accessibilityLabel={`${rotulo}: ${mascarado ? leituraMascarada(valor) : valor}`}
      style={{ gap: espaco.pequeno / 2 }}
    >
      <Texto variante="legenda">{rotulo}</Texto>
      <Texto variante="rotulo" selectable={selecionavel}>
        {valor}
      </Texto>
    </View>
  );
}
