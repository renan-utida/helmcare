import { ActivityIndicator, Pressable, StyleSheet, View } from 'react-native';

import { useTema } from '@/contextos/TemaContext';

import { Icone, type NomeIcone } from './Icone';
import { Texto } from './Texto';

/** Estilo do botão: ação principal, ação de apoio ou ação perigosa. */
export type VarianteBotao = 'primaria' | 'secundaria' | 'perigo';

/** Props do `Botao`. */
export interface BotaoProps {
  /** Texto visível do botão (também é o rótulo do leitor de tela, se não houver outro). */
  titulo: string;
  aoPressionar: () => void;
  variante?: VarianteBotao;
  /** Mostra o indicador de progresso e bloqueia novos toques. */
  carregando?: boolean;
  desabilitado?: boolean;
  /** Ícone à esquerda do texto (decorativo). */
  icone?: NomeIcone;
  /**
   * Botão de um grupo de escolha única (ex.: 4 h, 8 h, 12 h): o leitor de tela anuncia
   * "opção" e se está marcada. Sem este campo, é um botão comum.
   */
  selecionado?: boolean;
  accessibilityLabel?: string;
  accessibilityHint?: string;
  /** Altura mínima maior que a padrão (56 dp), para ações de emergência (ex.: 72 dp). */
  alturaMinima?: number;
  /**
   * Botão menor, para as ações secundárias de uma tela (ex.: "Estou bem, cancelar" na ocorrência):
   * altura mínima de 48 dp (a área de toque mínima) e letra de rótulo. Assim fica um botão principal
   * por tela, sem perder a área de toque.
   */
  compacto?: boolean;
}

/**
 * Botão grande e acessível: altura mínima de 56 dp, texto de 20 que escala com a fonte
 * do sistema e estados (desabilitado, carregando, selecionado) informados ao leitor de tela.
 */
export function Botao({
  titulo,
  aoPressionar,
  variante = 'primaria',
  carregando = false,
  desabilitado = false,
  icone,
  selecionado,
  accessibilityLabel,
  accessibilityHint,
  alturaMinima,
  compacto = false,
}: BotaoProps) {
  const { cores, espaco, raio, medidas } = useTema();
  const inativo = desabilitado || carregando;

  // Cor de fundo, de borda e do texto de cada variante. O selecionado inverte o secundário.
  const marcado = selecionado === true;
  const fundo =
    variante === 'primaria' || marcado ? cores.primaria : variante === 'perigo' ? cores.perigo : cores.fundo;
  const corTexto =
    variante === 'primaria' || marcado
      ? cores.textoSobrePrimaria
      : variante === 'perigo'
        ? cores.textoSobrePerigo
        : cores.primaria;
  const corBorda = variante === 'secundaria' && !marcado ? cores.primaria : fundo;

  return (
    <Pressable
      onPress={aoPressionar}
      disabled={inativo}
      accessibilityRole={selecionado === undefined ? 'button' : 'radio'}
      accessibilityLabel={accessibilityLabel ?? titulo}
      accessibilityHint={accessibilityHint}
      // Nas opções, `checked` dá o estado do papel "radio" e `selected` marca a escolhida.
      accessibilityState={{ disabled: inativo, busy: carregando, checked: selecionado, selected: selecionado }}
      style={({ pressed }) => [
        styles.base,
        {
          minHeight: Math.max(compacto ? medidas.toqueMinimo : medidas.alturaBotao, alturaMinima ?? 0),
          minWidth: medidas.toqueMinimo,
          paddingHorizontal: espaco.medio,
          paddingVertical: espaco.pequeno,
          borderRadius: raio.pequeno,
          backgroundColor: fundo,
          borderColor: corBorda,
          // Desabilitado fica mais claro (a WCAG dispensa o contraste de controles inativos)
          // e o estado é anunciado pelo leitor de tela. Carregando mantém a cor cheia.
          opacity: inativo && !carregando ? 0.55 : pressed ? 0.85 : 1,
        },
      ]}
    >
      <View style={[styles.conteudo, { gap: espaco.pequeno }]}>
        {carregando ? (
          <ActivityIndicator color={corTexto} accessibilityElementsHidden importantForAccessibility="no" />
        ) : icone ? (
          <Icone nome={icone} cor={corTexto} />
        ) : null}
        <Texto variante={compacto ? 'rotulo' : 'botao'} corPersonalizada={corTexto} centralizado style={styles.texto}>
          {titulo}
        </Texto>
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  base: {
    borderWidth: 2,
    alignItems: 'center',
    justifyContent: 'center',
  },
  conteudo: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    flexWrap: 'wrap',
  },
  texto: {
    flexShrink: 1,
  },
});
