import { router, Tabs } from 'expo-router';
import { Pressable, useWindowDimensions, type ColorValue } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { Icone, type NomeIcone } from '@/componentes/Icone';
import { useTema } from '@/contextos/TemaContext';

/** Botão de engrenagem do cabeçalho, que abre o modal de Configurações. */
function BotaoConfiguracoes() {
  const { cores, medidas } = useTema();
  return (
    <Pressable
      onPress={() => router.push('/configuracoes')}
      accessibilityRole="button"
      accessibilityLabel="Configurações"
      accessibilityHint="Abre a conexão com a API e os perfis do aparelho"
      hitSlop={8}
      style={{
        minWidth: medidas.toqueMinimo,
        minHeight: medidas.toqueMinimo,
        alignItems: 'center',
        justifyContent: 'center',
      }}
    >
      <Icone nome="settings-outline" cor={cores.texto} tamanho={28} />
    </Pressable>
  );
}

/** Ícone de uma aba (decorativo: o texto da aba já diz o nome). */
function iconeDaAba(nome: NomeIcone, corReserva: string) {
  function IconeAba({ color }: { color: ColorValue }) {
    // A barra devolve as cores do tema definidas abaixo, que são texto. O tipo também
    // admite número e cor nativa; nesses casos, usa a cor reserva do tema.
    return <Icone nome={nome} cor={typeof color === 'string' ? color : corReserva} />;
  }
  return IconeAba;
}

/**
 * Abas do app: Turno, Chamados, Histórico e Perfil, sempre com ícone e texto.
 * A altura da barra soma a área segura de baixo e cresce com a fonte do sistema,
 * para o texto das abas nunca ser cortado.
 */
export default function LayoutAbas() {
  const { cores, tipografia, espaco, medidas } = useTema();
  const insets = useSafeAreaInsets();
  const { fontScale } = useWindowDimensions();
  // Ícone + texto da legenda escalado (com folga de 1,4 por linha) + respiro em cima e embaixo.
  const alturaConteudo = medidas.icone + tipografia.legenda.fontSize * fontScale * 1.4 + espaco.medio;
  const alturaBarra = Math.max(medidas.alturaBotao + espaco.pequeno, alturaConteudo) + insets.bottom;

  return (
    <Tabs
      screenOptions={{
        headerStyle: { backgroundColor: cores.superficie },
        headerTintColor: cores.texto,
        headerTitleStyle: { color: cores.texto },
        headerRight: () => <BotaoConfiguracoes />,
        headerRightContainerStyle: { paddingRight: espaco.pequeno },
        tabBarActiveTintColor: cores.primaria,
        tabBarInactiveTintColor: cores.textoSecundario,
        tabBarLabelStyle: { fontSize: tipografia.legenda.fontSize, fontWeight: '700' },
        tabBarStyle: {
          backgroundColor: cores.superficie,
          borderTopColor: cores.borda,
          height: alturaBarra,
          paddingTop: espaco.pequeno / 2,
          paddingBottom: insets.bottom + espaco.pequeno / 2,
        },
        sceneStyle: { backgroundColor: cores.fundo },
      }}
    >
      <Tabs.Screen name="turno" options={{ title: 'Turno', tabBarIcon: iconeDaAba('speedometer', cores.textoSecundario) }} />
      <Tabs.Screen name="chamados" options={{ title: 'Chamados', tabBarIcon: iconeDaAba('people', cores.textoSecundario) }} />
      <Tabs.Screen name="historico" options={{ title: 'Histórico', tabBarIcon: iconeDaAba('time', cores.textoSecundario) }} />
      <Tabs.Screen name="perfil" options={{ title: 'Perfil', tabBarIcon: iconeDaAba('person-circle', cores.textoSecundario) }} />
    </Tabs>
  );
}
