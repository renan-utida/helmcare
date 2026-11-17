import { ActivityIndicator, Pressable, Switch, View } from 'react-native';

import { useTema } from '@/contextos/TemaContext';

import { Texto } from './Texto';

/** Props da `Chave`. */
export interface ChaveProps {
  /** Nome da chave, sempre visível e lido pelo leitor de tela. */
  rotulo: string;
  ligada: boolean;
  aoAlternar: () => void;
  /** Texto curto embaixo do rótulo (padrão: "Ligada" ou "Desligada"). */
  descricao?: string;
  desabilitada?: boolean;
  /** Mostra o indicador de progresso enquanto a mudança é enviada e bloqueia novos toques. */
  ocupada?: boolean;
  accessibilityHint?: string;
}

/**
 * Chave liga e desliga acessível: a linha inteira é a área de toque (56 dp ou mais) e o leitor de
 * tela lê uma vez só o nome, o estado (ligada ou desligada) e se está ocupada. O texto embaixo
 * repete o estado em palavras, para a cor da chave nunca ser a única pista.
 */
export function Chave({
  rotulo,
  ligada,
  aoAlternar,
  descricao,
  desabilitada = false,
  ocupada = false,
  accessibilityHint,
}: ChaveProps) {
  const { cores, espaco, medidas } = useTema();
  const inativa = desabilitada || ocupada;
  return (
    <Pressable
      onPress={aoAlternar}
      disabled={inativa}
      accessibilityRole="switch"
      accessibilityLabel={rotulo}
      accessibilityState={{ checked: ligada, disabled: inativa, busy: ocupada }}
      accessibilityHint={accessibilityHint}
      style={{ flexDirection: 'row', alignItems: 'center', gap: espaco.medio, minHeight: medidas.alturaBotao }}
    >
      <View style={{ flex: 1 }}>
        <Texto variante="rotulo">{rotulo}</Texto>
        <Texto variante="legenda">{descricao ?? (ligada ? 'Ligada' : 'Desligada')}</Texto>
      </View>
      {ocupada ? (
        <ActivityIndicator color={cores.primaria} accessibilityElementsHidden importantForAccessibility="no" />
      ) : null}
      <Switch
        value={ligada}
        onValueChange={aoAlternar}
        disabled={inativa}
        trackColor={{ false: cores.borda, true: cores.primaria }}
        thumbColor={cores.superficie}
        // A linha já anuncia a chave; o Switch não entra no leitor de novo.
        accessible={false}
        importantForAccessibility="no-hide-descendants"
      />
    </Pressable>
  );
}
