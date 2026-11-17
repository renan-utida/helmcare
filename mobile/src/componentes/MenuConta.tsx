import { router } from 'expo-router';
import { useEffect, useState } from 'react';
import { AccessibilityInfo, Animated, Modal, Pressable, ScrollView, useWindowDimensions, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useChamados } from '@/contextos/ChamadosContext';
import { useSessao } from '@/contextos/SessaoContext';
import { useTema } from '@/contextos/TemaContext';
import type { NomeEntregador } from '@/hooks/useNomeEntregador';

import { Botao } from './Botao';
import { Icone } from './Icone';
import { LinhaInfo } from './LinhaInfo';
import { Texto } from './Texto';

/** Duração da entrada do painel, em milissegundos. */
const DURACAO_ANIMACAO_MS = 220;
/** Largura máxima do painel, em dp. */
const LARGURA_MAXIMA = 340;

/** Props do `MenuConta`. */
export interface MenuContaProps {
  visivel: boolean;
  aoFechar: () => void;
  /** Nome do entregador ativo (só em memória), vindo do `useNomeEntregador`. */
  dadosNome: NomeEntregador;
}

/**
 * Painel lateral da conta, entrando pela direita (Modal transparente com Animated, sem
 * biblioteca nova). Fecha pelo X, pelo toque fora do painel e pelo voltar do Android.
 * Mostra o nome, o tipo do perfil, a participação na rede, "Trocar de conta" (por enquanto,
 * a tela de perfis; com o login da API, vira sair e entrar) e os códigos para suporte.
 */
export function MenuConta({ visivel, aoFechar, dadosNome }: MenuContaProps) {
  const { cores, espaco, raio, medidas } = useTema();
  const { perfilAtivo } = useSessao();
  const { voluntarioId, rede } = useChamados();
  const insets = useSafeAreaInsets();
  const { width } = useWindowDimensions();
  const largura = Math.min(width * 0.85, LARGURA_MAXIMA);
  // Valor animado estável, criado uma vez (useState com inicializador, sem ler ref na renderização).
  const [deslocamento] = useState(() => new Animated.Value(largura));

  // Entrada pela direita sempre que o painel abre.
  useEffect(() => {
    if (!visivel) {
      return;
    }
    deslocamento.setValue(largura);
    Animated.timing(deslocamento, { toValue: 0, duration: DURACAO_ANIMACAO_MS, useNativeDriver: true }).start();
    AccessibilityInfo.announceForAccessibility('Menu da conta aberto');
  }, [visivel, largura, deslocamento]);

  if (!perfilAtivo) {
    return null;
  }

  const { nome, erro } = dadosNome;
  const tipo = perfilAtivo.codigoCapacete ? `Entregador com capacete ${perfilAtivo.codigoCapacete}` : 'Voluntário';
  // Disponibilidade da rede neste aparelho (ChamadosContext): ao abrir o app, começa indisponível.
  const situacaoRede = voluntarioId ? (rede.disponivel ? 'disponível' : 'indisponível') : 'não participa';

  function irPara(destino: '/perfis' | '/perfil') {
    aoFechar();
    if (destino === '/perfis') {
      router.push('/perfis');
    } else {
      router.navigate('/perfil');
    }
  }

  return (
    <Modal visible={visivel} transparent animationType="fade" onRequestClose={aoFechar} statusBarTranslucent>
      <View style={{ flex: 1, flexDirection: 'row' }}>
        {/* Toque fora do painel fecha o menu. */}
        <Pressable
          onPress={aoFechar}
          accessibilityRole="button"
          accessibilityLabel="Fechar o menu da conta"
          style={{ flex: 1, backgroundColor: cores.sombraModal }}
        />
        <Animated.View
          accessibilityViewIsModal
          style={{
            width: largura,
            backgroundColor: cores.fundo,
            transform: [{ translateX: deslocamento }],
            borderTopLeftRadius: raio.medio,
            borderBottomLeftRadius: raio.medio,
          }}
        >
          <ScrollView
            contentContainerStyle={{
              gap: espaco.grande,
              paddingTop: insets.top + espaco.medio,
              paddingBottom: insets.bottom + espaco.grande,
              paddingHorizontal: espaco.medio,
            }}
          >
            <View style={{ flexDirection: 'row', justifyContent: 'flex-end' }}>
              <Pressable
                onPress={aoFechar}
                accessibilityRole="button"
                accessibilityLabel="Fechar o menu da conta"
                hitSlop={8}
                style={{
                  minWidth: medidas.toqueMinimo,
                  minHeight: medidas.toqueMinimo,
                  alignItems: 'center',
                  justifyContent: 'center',
                }}
              >
                <Icone nome="close" cor={cores.texto} tamanho={28} />
              </Pressable>
            </View>

            <View style={{ gap: espaco.pequeno }}>
              <Texto variante="titulo">{nome ?? perfilAtivo.apelido}</Texto>
              <Texto cor="textoSecundario">{tipo}</Texto>
              {erro === 'sem-conexao' ? (
                <Texto variante="legenda" cor="erro" accessibilityLiveRegion="polite">
                  Sem conexão: não deu para carregar seus dados.
                </Texto>
              ) : erro ? (
                <Texto variante="legenda" cor="erro" accessibilityLiveRegion="polite">
                  Não deu para carregar seus dados agora.
                </Texto>
              ) : null}
            </View>

            <LinhaInfo rotulo="Voluntário da rede" valor={situacaoRede} />

            <View style={{ gap: espaco.medio }}>
              <Botao
                titulo="Trocar de conta"
                icone="swap-horizontal"
                aoPressionar={() => irPara('/perfis')}
                accessibilityHint="Abre os perfis deste aparelho"
              />
              <Botao titulo="Ver meu perfil" variante="secundaria" icone="person" aoPressionar={() => irPara('/perfil')} />
            </View>

            <View style={{ gap: espaco.pequeno, borderTopWidth: 1, borderTopColor: cores.trilha, paddingTop: espaco.medio }}>
              <Texto variante="rotulo" accessibilityRole="header">
                Para suporte
              </Texto>
              <Texto variante="legenda" selectable accessibilityLabel={`Código do cadastro: ${perfilAtivo.entregadorId}`}>
                {`Código do cadastro: ${perfilAtivo.entregadorId}`}
              </Texto>
              <Texto variante="legenda">
                {`Código do capacete: ${perfilAtivo.codigoCapacete ?? 'sem capacete'}`}
              </Texto>
            </View>
          </ScrollView>
        </Animated.View>
      </View>
    </Modal>
  );
}
