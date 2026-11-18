import { Tabs } from 'expo-router';
import { useState } from 'react';
import { Pressable, useWindowDimensions, View, type ColorValue } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { Icone, type NomeIcone } from '@/componentes/Icone';
import { SimboloMarca } from '@/componentes/Marca';
import { MenuConta } from '@/componentes/MenuConta';
import { Texto } from '@/componentes/Texto';
import { useChamados } from '@/contextos/ChamadosContext';
import { useSessao } from '@/contextos/SessaoContext';
import { useTema } from '@/contextos/TemaContext';

/**
 * Único botão do lado direito do cabeçalho: o ícone de pessoa, que abre o menu da conta
 * (painel lateral). O nome vem da sessão, só em memória; enquanto não chega (ou sem conexão),
 * o rótulo diz "Conta salva".
 */
function BotaoConta() {
  const { cores, medidas } = useTema();
  const { contaAtiva, sessao } = useSessao();
  const [aberto, setAberto] = useState(false);
  if (!contaAtiva) {
    return null;
  }
  return (
    <>
      <Pressable
        onPress={() => setAberto(true)}
        accessibilityRole="button"
        accessibilityLabel={`Menu da conta: ${sessao?.nome ?? 'Conta salva'}`}
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
      <MenuConta visivel={aberto} aoFechar={() => setAberto(false)} />
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
 * A aba Chamados mostra no ícone o número de chamados próximos (com o voluntário disponível),
 * e o leitor de tela diz esse número junto com o nome da aba.
 */
export default function LayoutAbas() {
  const { cores, tipografia, espaco, medidas } = useTema();
  const { chamados } = useChamados();
  const quantidadeChamados = chamados?.length ?? 0;
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
      {ABAS.map((aba, indice) => {
        const selo = aba.nome === 'chamados' && quantidadeChamados > 0 ? quantidadeChamados : undefined;
        const leituraSelo =
          selo === undefined ? '' : `, ${selo} ${selo === 1 ? 'chamado de socorro' : 'chamados de socorro'}`;
        return (
          <Tabs.Screen
            key={aba.nome}
            name={aba.nome}
            options={{
              title: aba.titulo,
              tabBarIcon: iconeDaAba(aba.icone, cores.abaInativa),
              tabBarLabel: rotuloDaAba(aba.titulo, cores.abaInativa),
              // Número de chamados no ícone; o número também vai em palavras no rótulo abaixo.
              tabBarBadge: selo,
              // O selo da biblioteca tem altura fixa de 18 dp: com `height: 'auto'`, ele cresce
              // junto com a fonte do sistema e o número não é cortado.
              tabBarBadgeStyle: {
                backgroundColor: cores.perigo,
                color: cores.textoSobrePerigo,
                height: 'auto',
                minHeight: 18,
                paddingHorizontal: espaco.pequeno / 2,
              },
              // O padrão da biblioteca vem em inglês ("tab, 1 of 5"): aqui, em português.
              tabBarAccessibilityLabel: `${aba.titulo}${leituraSelo}, aba ${indice + 1} de ${ABAS.length}`,
            }}
          />
        );
      })}
    </Tabs>
  );
}
