import { View } from 'react-native';

import { useTema } from '@/contextos/TemaContext';

import { Texto } from './Texto';

/**
 * Transforma um valor mascarado pela API num texto bom de ouvir. Em vez de o leitor de tela
 * dizer "asterisco" várias vezes, diz, por exemplo, "DDD 11, parte oculta, termina em 1 2 3 4".
 * Não desmascara nada: usa só o que a API já mostrou. O e-mail mascarado ("r***@exemplo.com")
 * vira "começa com r, parte oculta, arroba exemplo.com".
 */
export function leituraMascarada(valor: string): string {
  if (!valor.includes('*')) {
    return valor;
  }
  const email = valor.match(/^([^*@]*)\*+@(.+)$/);
  if (email) {
    const inicio = email[1] ? `começa com ${email[1].split('').join(' ')}, ` : '';
    return `${inicio}parte oculta, arroba ${email[2]}`;
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
  /** Como o leitor de tela fala o valor, quando difere do texto (ex.: "A+" vira "A positivo"). */
  leitura?: string;
}

/** Par rótulo e valor, lido como uma frase só ("CPF: parte oculta, termina em 1 2"). */
export function LinhaInfo({ rotulo, valor, mascarado = false, selecionavel = false, leitura }: LinhaInfoProps) {
  const { espaco } = useTema();
  return (
    <View
      accessible
      accessibilityLabel={`${rotulo}: ${leitura ?? (mascarado ? leituraMascarada(valor) : valor)}`}
      style={{ gap: espaco.pequeno / 2 }}
    >
      <Texto variante="legenda">{rotulo}</Texto>
      <Texto variante="rotulo" selectable={selecionavel}>
        {valor}
      </Texto>
    </View>
  );
}
