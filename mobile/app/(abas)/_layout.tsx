import { Tabs } from 'expo-router';
import { useState } from 'react';
import { Pressable, useWindowDimensions, View, type ColorValue } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { Icone, type NomeIcone } from '@/componentes/Icone';
import { SimboloMarca } from '@/componentes/Marca';
import { MenuConta } from '@/componentes/MenuConta';
import { Texto } from '@/componentes/Texto';
import { useSessao } from '@/contextos/SessaoContext';
import { useTema } from '@/contextos/TemaContext';
import { useNomeEntregador } from '@/hooks/useNomeEntregador';

/**
 * Único botão do lado direito do cabeçalho: o ícone de pessoa, que abre o menu da conta
 * (painel lateral). O nome vem do `GET /entregadores/{id}`, só em memória; enquanto não
 * chega (ou sem conexão), o rótulo usa o apelido.
 */
function BotaoConta() {
  const { cores, medidas } = useTema();
  const { perfilAtivo } = useSessao();
  const dadosNome = useNomeEntregador(perfilAtivo?.entregadorId ?? null);
  const [aberto, setAberto] = useState(false);
  if (!perfilAtivo) {
    return null;
  }
  return (
    <>
      <Pressable
        onPress={() => setAberto(true)}
        accessibilityRole="button"
        accessibilityLabel={`Menu da conta: ${dadosNome.nome ?? perfilAtivo.apelido}`}
        accessibilityHint="Abre seus dados, a troca de conta e os códigos para suporte"
        hitSlop={8}
        style={{
          minWidth: medidas.toqueMinimo,
          minHeight: medidas.toqueMinimo,
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        <Icone nome="person-circle-outline" cor={cores.texto} tamanho={32} />
      </Pressable>
      <MenuConta visivel={aberto} aoFechar={() => setAberto(false)} dadosNome={dadosNome} />
    </>
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
 * Rótulo de uma aba. Com 5 abas e a fonte no máximo, "Histórico" e "Chamados" não cabem na
 * largura de uma aba: o texto cresce com a fonte do sistema e, só quando passaria da largura,
 * encolhe até 60% para caber inteiro (em vez de terminar em reticências).
 */
function rotuloDaAba(texto: string, corReserva: string) {
  function RotuloAba({ color }: { color: ColorValue }) {
    return (
      <Texto
        variante="legenda"
        corPersonalizada={typeof color === 'string' ? color : corReserva}
        numberOfLines={1}
        adjustsFontSizeToFit
        minimumFontScale={0.6}
        style={{ fontWeight: '700' }}
      >
        {texto}
      </Texto>
    );
  }
  return RotuloAba;
}

/** As 5 abas, na ordem da barra, com o ícone de cada uma. */
const ABAS: { nome: string; titulo: string; icone: NomeIcone }[] = [
  { nome: 'turno', titulo: 'Turno', icone: 'speedometer' },
  { nome: 'chamados', titulo: 'Chamados', icone: 'people' },
  { nome: 'historico', titulo: 'Histórico', icone: 'time' },
  { nome: 'perfil', titulo: 'Perfil', icone: 'person-circle' },
  { nome: 'ajustes', titulo: 'Ajustes', icone: 'settings' },
];

/**
 * Abas do app: Turno, Chamados, Histórico, Perfil e Ajustes, sempre com ícone e texto.
 * A altura da barra soma a área segura de baixo e cresce com a fonte do sistema,
 * para o texto das abas nunca ser cortado. No cabeçalho, só o botão do menu da conta.
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
        headerRight: () => <BotaoConta />,
        headerRightContainerStyle: { paddingRight: espaco.pequeno },
        tabBarActiveTintColor: cores.abaAtiva,
        tabBarInactiveTintColor: cores.abaInativa,
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
      {ABAS.map((aba, indice) => (
        <Tabs.Screen
          key={aba.nome}
          name={aba.nome}
          options={{
            title: aba.titulo,
            tabBarIcon: iconeDaAba(aba.icone, cores.abaInativa),
            tabBarLabel: rotuloDaAba(aba.titulo, cores.abaInativa),
            // O padrão da biblioteca vem em inglês ("tab, 1 of 5"): aqui, em português.
            tabBarAccessibilityLabel: `${aba.titulo}, aba ${indice + 1} de ${ABAS.length}`,
          }}
        />
      ))}
    </Tabs>
  );
}
