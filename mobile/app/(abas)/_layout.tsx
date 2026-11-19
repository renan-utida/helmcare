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
 * Largura do maior rótulo ("Histórico") em "em" (múltiplos do tamanho da fonte), em Roboto
 * negrito, com folga. Serve para saber até onde os rótulos podem crescer sem passar da aba.
 */
const LARGURA_EM_DO_MAIOR_ROTULO = 4.3;

/**
 * Rótulo de uma aba. Antes, cada rótulo encolhia sozinho para caber (`adjustsFontSizeToFit`), e
 * "Chamados", a palavra mais larga, saía menor e fora da linha de base das outras. Agora os 5
 * usam o mesmo estilo, sem encolher, e crescem com a fonte do sistema até o mesmo limite
 * (`maxFontSizeMultiplier`, igual para os cinco): quando o maior rótulo enche a aba, todos param
 * de crescer juntos. Assim os 5 têm sempre o mesmo tamanho e a mesma linha de base.
 */
function rotuloDaAba(texto: string, corReserva: string, multiplicadorMaximo: number) {
  function RotuloAba({ color }: { color: ColorValue }) {
    return (
      <Texto
        variante="aba"
        corPersonalizada={typeof color === 'string' ? color : corReserva}
        numberOfLines={1}
        maxFontSizeMultiplier={multiplicadorMaximo}
        centralizado
      >
        {texto}
      </Texto>
    );
  }
  return RotuloAba;
}

/** As 5 abas, na ordem da barra, com o ícone de cada uma. */
const ABAS: { nome: string; titulo: string; rotulo: string; icone: NomeIcone }[] = [
  { nome: 'inicio', titulo: 'Início', rotulo: 'Início', icone: 'home' },
  // "Chamados" não cabe na largura de uma aba com a fonte no máximo, mesmo sem espaço nas
  // laterais: o rótulo da aba é "Rede", e o título da tela continua "Rede de socorro".
  { nome: 'chamados', titulo: 'Rede de socorro', rotulo: 'Rede', icone: 'people' },
  { nome: 'historico', titulo: 'Histórico', rotulo: 'Histórico', icone: 'time' },
  { nome: 'perfil', titulo: 'Perfil', rotulo: 'Perfil', icone: 'person-circle' },
  { nome: 'ajustes', titulo: 'Ajustes', rotulo: 'Ajustes', icone: 'settings' },
];

/**
 * Abas do app: Início, Chamados, Histórico, Perfil e Ajustes, sempre com ícone e texto.
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
  const { fontScale, width } = useWindowDimensions();
  // Até quanto os rótulos crescem: o maior rótulo ocupa no máximo a largura de uma aba.
  const larguraAba = width / ABAS.length;
  const multiplicadorRotulo = Math.max(1, larguraAba / (tipografia.aba.fontSize * LARGURA_EM_DO_MAIOR_ROTULO));
  const escalaRotulo = Math.min(fontScale, multiplicadorRotulo);
  // Ícone + rótulo escalado (com folga de 1,4 por linha) + respiro em cima e embaixo.
  const alturaConteudo = medidas.icone + tipografia.aba.fontSize * escalaRotulo * 1.4 + espaco.medio;
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
        // Sem espaço nas laterais de cada item: a largura inteira da aba fica para o rótulo.
        tabBarItemStyle: { paddingHorizontal: 0 },
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
              tabBarLabel: rotuloDaAba(aba.rotulo, cores.abaInativa, multiplicadorRotulo),
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
