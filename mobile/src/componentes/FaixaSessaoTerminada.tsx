import { router } from 'expo-router';
import { View } from 'react-native';

import { useSessao } from '@/contextos/SessaoContext';
import { useTema } from '@/contextos/TemaContext';

import { Aviso } from './Aviso';
import { Botao } from './Botao';
import { BotaoLigar192 } from './BotaoLigar192';

/** Props da `FaixaSessaoTerminada`. */
export interface FaixaSessaoTerminadaProps {
  /** Mostra também o "Ligar 192" na faixa (no alerta, que não tem o botão na própria tela). */
  mostrar192?: boolean;
  /** Espaço extra no topo (a área da barra de status, no alerta em tela cheia). */
  espacoTopo?: number;
}

/**
 * Faixa fixa no topo do alerta de queda e da ocorrência quando a sessão da conta termina (a
 * renovação deu 401): a tela não fecha, para não atrapalhar um alerta ou um SOS. O botão "Entrar"
 * abre o login por cima e, depois de entrar, volta para a mesma tela. Sem sessão terminada, não
 * mostra nada.
 */
export function FaixaSessaoTerminada({ mostrar192 = false, espacoTopo = 0 }: FaixaSessaoTerminadaProps) {
  const { cores, espaco } = useTema();
  const { estadoSessao } = useSessao();
  if (estadoSessao !== 'terminada') {
    return null;
  }
  return (
    <View
      style={{
        gap: espaco.pequeno,
        padding: espaco.medio,
        paddingTop: espaco.medio + espacoTopo,
        backgroundColor: cores.fundo,
        borderBottomWidth: 1,
        borderBottomColor: cores.trilha,
      }}
    >
      <Aviso tipo="atencao" mensagem="Sua sessão terminou. Entre de novo para continuar acompanhando." />
      <Botao
        titulo="Entrar"
        icone="log-in"
        aoPressionar={() => router.push({ pathname: '/login', params: { modo: 'reentrar' } })}
        accessibilityHint="Abre o login. Depois de entrar, você volta para esta tela."
      />
      {mostrar192 ? <BotaoLigar192 /> : null}
    </View>
  );
}
