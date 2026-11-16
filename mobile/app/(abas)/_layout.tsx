import { router, Tabs } from 'expo-router';
import { Pressable, useWindowDimensions, View, type ColorValue } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { Icone, type NomeIcone } from '@/componentes/Icone';
import { SimboloMarca } from '@/componentes/Marca';
import { Texto } from '@/componentes/Texto';
import { useSessao } from '@/contextos/SessaoContext';
import { useTema } from '@/contextos/TemaContext';

/** Botão de engrenagem do cabeçalho, que abre o modal de Configurações. */
function BotaoConfiguracoes() {
  const { cores, medidas } = useTema();
  return (
    <Pressable
      onPress={() => router.push('/configuracoes')}
      accessibilityRole="button"
      accessibilityLabel="Configurações"
      accessibilityHint="Abre a conexão com a API, a aparência e os perfis do aparelho"
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

/** Acima desta escala de fonte, o botão do perfil mostra só o ícone, para o título caber. */
const ESCALA_MAXIMA_COM_APELIDO = 1.3;

/**
 * Botão do perfil ativo, à esquerda da engrenagem: ícone de pessoa e o apelido.
 * Com a fonte do sistema muito grande, mostra só o ícone (o apelido continua no rótulo
 * do leitor de tela) e deixa o espaço do cabeçalho para o nome da aba.
 */
function BotaoPerfilAtivo() {
  const { cores, espaco, raio, medidas } = useTema();
  const { perfilAtivo } = useSessao();
  const { width, fontScale } = useWindowDimensions();
  if (!perfilAtivo) {
    return null;
  }
  const mostrarApelido = fontScale <= ESCALA_MAXIMA_COM_APELIDO;
  return (
    <Pressable
      onPress={() => router.push('/perfis')}
      accessibilityRole="button"
      accessibilityLabel={`Perfil ativo: ${perfilAtivo.apelido}. Toque para ver os perfis.`}
      style={({ pressed }) => ({
        flexDirection: 'row',
        alignItems: 'center',
        gap: espaco.pequeno / 2,
        minHeight: medidas.toqueMinimo,
        minWidth: medidas.toqueMinimo,
        // Nunca passa de 40% da largura: o nome da aba fica com o resto.
        maxWidth: width * 0.4,
        justifyContent: 'center',
        paddingHorizontal: espaco.pequeno,
        borderRadius: raio.pequeno,
        borderWidth: 1,
        borderColor: cores.borda,
        opacity: pressed ? 0.85 : 1,
      })}
    >
      <Icone nome="person" cor={cores.texto} />
      {mostrarApelido ? (
        <Texto variante="legenda" cor="texto" numberOfLines={1} style={{ flexShrink: 1 }}>
          {perfilAtivo.apelido}
        </Texto>
      ) : null}
    </Pressable>
  );
}

/** Lado direito do cabeçalho: o perfil ativo e a engrenagem das Configurações. */
function AcoesCabecalho() {
  const { espaco } = useTema();
  return (
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: espaco.pequeno }}>
      <BotaoPerfilAtivo />
      <BotaoConfiguracoes />
    </View>
  );
}

/** Título do cabeçalho das abas: o símbolo do HelmCare à esquerda do nome da aba. */
function TituloComSimbolo({ children }: { children: string }) {
  const { espaco } = useTema();
  return (
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: espaco.pequeno }}>
      <SimboloMarca />
      {/* O cabeçalho tem altura fixa no React Navigation: com fonte grande, o nome curto da
          aba termina em reticências em vez de ser cortado no meio. */}
      <Texto variante="rotulo" accessibilityRole="header" numberOfLines={1} style={{ flexShrink: 1 }}>
        {children}
      </Texto>
    </View>
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
        headerTitleAlign: 'left',
        headerTitle: ({ children }) => <TituloComSimbolo>{children}</TituloComSimbolo>,
        headerRight: () => <AcoesCabecalho />,
        headerRightContainerStyle: { paddingRight: espaco.pequeno },
        tabBarActiveTintColor: cores.abaAtiva,
        tabBarInactiveTintColor: cores.abaInativa,
        tabBarLabelStyle: { fontSize: tipografia.legenda.fontSize, fontWeight: '700' },
        tabBarStyle: {
          backgroundColor: cores.superficie,
          // Faixa laranja da marca no topo da barra: só enfeite, não carrega informação.
          borderTopColor: cores.destaque,
          borderTopWidth: 3,
          height: alturaBarra,
          paddingTop: espaco.pequeno / 2,
          paddingBottom: insets.bottom + espaco.pequeno / 2,
        },
        sceneStyle: { backgroundColor: cores.fundo },
      }}
    >
      <Tabs.Screen name="turno" options={{ title: 'Turno', tabBarIcon: iconeDaAba('speedometer', cores.abaInativa) }} />
      <Tabs.Screen name="chamados" options={{ title: 'Chamados', tabBarIcon: iconeDaAba('people', cores.abaInativa) }} />
      <Tabs.Screen name="historico" options={{ title: 'Histórico', tabBarIcon: iconeDaAba('time', cores.abaInativa) }} />
      <Tabs.Screen name="perfil" options={{ title: 'Perfil', tabBarIcon: iconeDaAba('person-circle', cores.abaInativa) }} />
    </Tabs>
  );
}
