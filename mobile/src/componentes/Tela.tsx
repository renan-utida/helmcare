import type { ReactNode, RefObject } from 'react';
import { KeyboardAvoidingView, RefreshControl, ScrollView, View, type ScrollViewInstance } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useTema } from '@/contextos/TemaContext';

/** Props da `Tela`. */
export interface TelaProps {
  children: ReactNode;
  /** Ação do "puxar para atualizar". Sem ela, a tela não tem esse gesto. */
  aoAtualizar?: () => void;
  /** Mostra o indicador do "puxar para atualizar". */
  atualizando?: boolean;
  /**
   * Verdadeiro nas abas: a barra de abas já cuida da área segura de baixo.
   * Nas telas da pilha e nos modais, a tela soma a área da barra de navegação do Android.
   */
  dentroDeAbas?: boolean;
  /**
   * Soma a área segura do topo (a barra de status do Android) com um respiro: para as telas sem
   * cabeçalho, como o login no modo "entrar", em que nada pode ficar por baixo do relógio.
   */
  areaSeguraTopo?: boolean;
  /** Referência da rolagem, para a tela rolar até uma seção (ex.: os contatos do Perfil). */
  refRolagem?: RefObject<ScrollViewInstance | null>;
}

/**
 * Base de todas as telas: fundo do tema, rolagem (para a fonte grande nunca cortar texto),
 * teclado sem cobrir os campos e espaçamento padrão.
 */
export function Tela({
  children,
  aoAtualizar,
  atualizando = false,
  dentroDeAbas = false,
  areaSeguraTopo = false,
  refRolagem,
}: TelaProps) {
  const { cores, espaco } = useTema();
  const insets = useSafeAreaInsets();
  return (
    // "padding" também no Android: com a tela de borda a borda, a janela não encolhe sozinha.
    <KeyboardAvoidingView behavior="padding" style={{ flex: 1, backgroundColor: cores.fundo }}>
      <ScrollView
        ref={refRolagem}
        keyboardShouldPersistTaps="handled"
        contentContainerStyle={{
          flexGrow: 1,
          padding: espaco.medio,
          paddingTop: areaSeguraTopo ? insets.top + espaco.grande : espaco.medio,
          paddingBottom: espaco.grande + (dentroDeAbas ? 0 : insets.bottom),
        }}
        refreshControl={
          aoAtualizar ? (
            <RefreshControl
              refreshing={atualizando}
              onRefresh={aoAtualizar}
              colors={[cores.primaria]}
              progressBackgroundColor={cores.superficie}
            />
          ) : undefined
        }
      >
        <View style={{ gap: espaco.grande }}>{children}</View>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}
