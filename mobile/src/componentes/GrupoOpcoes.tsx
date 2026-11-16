import { View } from 'react-native';

import { useTema } from '@/contextos/TemaContext';

import { Botao } from './Botao';
import { Texto } from './Texto';

/** Props do `GrupoOpcoes`. */
export interface GrupoOpcoesProps<T extends string> {
  /** Pergunta ou rótulo do grupo, sempre visível. */
  rotulo: string;
  opcoes: readonly T[];
  selecionada: T | null;
  aoSelecionar: (opcao: T) => void;
  /** Mensagem de erro do grupo (ex.: nenhuma opção escolhida). */
  erro?: string | null;
  /** Texto de cada opção (padrão: a própria opção). */
  textoDe?: (opcao: T) => string;
  /** Como o leitor de tela fala cada opção, quando difere do texto (ex.: "A+" vira "A positivo"). */
  leituraDe?: (opcao: T) => string;
  /** Quantas opções por linha (padrão: 2). */
  colunas?: number;
}

/**
 * Escolha única em botões grandes, no lugar de listas e seletores pequenos.
 * Cada botão anuncia se está selecionado; o grupo é lido como lista de opções.
 */
export function GrupoOpcoes<T extends string>({
  rotulo,
  opcoes,
  selecionada,
  aoSelecionar,
  erro,
  textoDe = (opcao) => opcao,
  leituraDe,
  colunas = 2,
}: GrupoOpcoesProps<T>) {
  const { espaco } = useTema();
  return (
    <View style={{ gap: espaco.pequeno }}>
      <Texto variante="rotulo">{rotulo}</Texto>
      <View accessibilityRole="radiogroup" style={{ flexDirection: 'row', flexWrap: 'wrap', gap: espaco.pequeno }}>
        {opcoes.map((opcao) => (
          // A largura em porcentagem divide a linha; o gap entra no cálculo pelo flexGrow.
          <View key={opcao} style={{ flexBasis: `${Math.floor(100 / colunas) - 4}%`, flexGrow: 1 }}>
            <Botao
              titulo={textoDe(opcao)}
              variante="secundaria"
              selecionado={selecionada === opcao}
              aoPressionar={() => aoSelecionar(opcao)}
              accessibilityLabel={`${rotulo}: ${(leituraDe ?? textoDe)(opcao)}`}
            />
          </View>
        ))}
      </View>
      {erro ? (
        <Texto variante="legenda" cor="erro" accessibilityLiveRegion="polite">
          {`Erro: ${erro}`}
        </Texto>
      ) : null}
    </View>
  );
}
