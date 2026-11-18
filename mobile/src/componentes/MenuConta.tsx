import { router } from 'expo-router';
import { useEffect, useState } from 'react';
import { AccessibilityInfo, Alert, Animated, Modal, Pressable, ScrollView, useWindowDimensions, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useChamados } from '@/contextos/ChamadosContext';
import { useSessao } from '@/contextos/SessaoContext';
import { useTema } from '@/contextos/TemaContext';
import { comecarPilhaEm } from '@/utils/navegacao';

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
}

/** Subtítulo da conta pela sessão: "Capacete HC-0001", "Voluntário da rede" ou os dois. */
export function tipoDaConta(codigoDispositivo: string | null, voluntarioId: string | null): string {
  const partes = [
    codigoDispositivo ? `Capacete ${codigoDispositivo}` : null,
    voluntarioId ? 'Voluntário da rede' : null,
  ].filter((parte) => parte !== null);
  return partes.length > 0 ? partes.join(' e ') : 'Sem capacete';
}

/**
 * Painel lateral da conta, entrando pela direita (Modal transparente com Animated, sem
 * biblioteca nova). Fecha pelo X, pelo toque fora do painel e pelo voltar do Android.
 * Mostra o nome (da sessão, só em memória), o tipo da conta, a participação na rede,
 * "Trocar de conta" (as contas salvas no aparelho), "Ver meu perfil", "Sair" (com confirmação)
 * e os códigos para suporte.
 */
export function MenuConta({ visivel, aoFechar }: MenuContaProps) {
  const { cores, espaco, raio, medidas } = useTema();
  const { contaAtiva, sessao, estadoSessao, sair } = useSessao();
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

  if (!contaAtiva) {
    return null;
  }

  const tipo = sessao ? tipoDaConta(sessao.codigoDispositivo, sessao.voluntarioId) : null;
  // Disponibilidade da rede neste aparelho (ChamadosContext): ao abrir o app, começa indisponível.
  const situacaoRede = voluntarioId ? (rede.disponivel ? 'disponível' : 'indisponível') : 'não participa';

  function irPara(destino: '/contas' | '/perfil') {
    aoFechar();
    if (destino === '/contas') {
      router.push('/contas');
    } else {
      router.navigate('/perfil');
    }
  }

  async function sairDaConta(entregadorId: string) {
    aoFechar();
    const proxima = await sair(entregadorId);
    AccessibilityInfo.announceForAccessibility('Você saiu da conta');
    if (proxima) {
      router.dismissTo('/inicio');
    } else {
      comecarPilhaEm('/login');
    }
  }

  function confirmarSair(entregadorId: string) {
    Alert.alert(
      'Sair desta conta?',
      'A conta sai deste celular. O histórico salvo aqui continua e volta quando você entrar de novo.',
      [
        { text: 'Cancelar', style: 'cancel' },
        { text: 'Sair', style: 'destructive', onPress: () => void sairDaConta(entregadorId) },
      ],
    );
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
              <Texto variante="titulo">{sessao?.nome ?? 'Conta salva'}</Texto>
              {tipo ? <Texto cor="textoSecundario">{tipo}</Texto> : null}
              {estadoSessao === 'sem-conexao' ? (
                <Texto variante="legenda" cor="erro" accessibilityLiveRegion="polite">
                  Sem conexão: não deu para carregar seus dados.
                </Texto>
              ) : estadoSessao === 'erro' ? (
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
                aoPressionar={() => irPara('/contas')}
                accessibilityHint="Abre as contas salvas neste celular"
              />
              <Botao titulo="Ver meu perfil" variante="secundaria" icone="person" aoPressionar={() => irPara('/perfil')} />
              <Botao
                titulo="Sair"
                variante="secundaria"
                icone="log-out"
                aoPressionar={() => confirmarSair(contaAtiva)}
                accessibilityHint="Pede confirmação antes de tirar a conta deste celular"
              />
            </View>

            <View style={{ gap: espaco.pequeno, borderTopWidth: 1, borderTopColor: cores.trilha, paddingTop: espaco.medio }}>
              <Texto variante="rotulo" accessibilityRole="header">
                Para suporte
              </Texto>
              <Texto variante="legenda" selectable accessibilityLabel={`Código do cadastro: ${contaAtiva}`}>
                {`Código do cadastro: ${contaAtiva}`}
              </Texto>
              <Texto variante="legenda">
                {`Código do capacete: ${sessao ? (sessao.codigoDispositivo ?? 'sem capacete') : 'não carregado'}`}
              </Texto>
            </View>
          </ScrollView>
        </Animated.View>
      </View>
    </Modal>
  );
}
