import {
  AndroidImportance,
  getPermissionsAsync,
  requestPermissionsAsync,
  scheduleNotificationAsync,
  setNotificationChannelAsync,
  setNotificationHandler,
  type NotificationResponse,
} from 'expo-notifications';
import { Platform } from 'react-native';

import type { PapelOcorrencia } from '@/banco/ocorrenciaRepositorio';
import type { FaixaRisco } from '@/tipos/contrato';

/**
 * Notificações LOCAIS do app (expo-notifications). O push remoto não funciona no Expo Go, então
 * tudo é disparado pelo próprio app, a partir do que o polling lê. Nenhum dado pessoal vai no
 * texto (contrato, seção 9): nada de nome, telefone, ficha ou localização. Sem permissão, o app
 * funciona igual, só não notifica.
 */

/** Canal Android dos alertas do HelmCare, de alta importância (som, vibração e banner). */
export const CANAL_ALERTAS = 'alertas';

/** Para onde a notificação leva quando a pessoa toca nela. */
export type DestinoNotificacao =
  | { tela: 'alerta' }
  | { tela: 'ocorrencia'; ocorrenciaId: string; papel: PapelOcorrencia }
  | { tela: 'chamados' }
  | { tela: 'turno' };

/** Situação da permissão: ainda não pedida, concedida ou negada. */
export type PermissaoNotificacao = 'nao-pedida' | 'concedida' | 'negada';

/**
 * Prepara as notificações ao abrir o app: com o app aberto, a notificação também aparece no topo
 * (banner e lista), e no Android o canal "alertas" é criado com importância alta.
 */
export async function configurarNotificacoes(): Promise<void> {
  setNotificationHandler({
    handleNotification: async () => ({
      shouldShowBanner: true,
      shouldShowList: true,
      shouldPlaySound: true,
      shouldSetBadge: false,
    }),
  });
  if (Platform.OS === 'android') {
    await setNotificationChannelAsync(CANAL_ALERTAS, {
      name: 'Alertas do HelmCare',
      importance: AndroidImportance.HIGH,
      vibrationPattern: [0, 400, 200, 400],
    });
  }
}

/** Lê a permissão sem pedir nada. */
export async function lerPermissao(): Promise<PermissaoNotificacao> {
  const { status } = await getPermissionsAsync();
  return status === 'granted' ? 'concedida' : status === 'undetermined' ? 'nao-pedida' : 'negada';
}

/** Pede a permissão ao sistema. Devolve se ficou concedida. */
export async function pedirPermissao(): Promise<boolean> {
  const { status } = await requestPermissionsAsync();
  return status === 'granted';
}

/** Mostra uma notificação agora, no canal "alertas", se a permissão estiver concedida. */
async function notificar(titulo: string, corpo: string, destino: DestinoNotificacao): Promise<void> {
  try {
    if ((await lerPermissao()) !== 'concedida') {
      return;
    }
    await scheduleNotificationAsync({
      content: { title: titulo, body: corpo, sound: true, data: { destino } },
      // `channelId` sem horário: aparece na hora, no canal de alta importância (no iOS, o canal é ignorado).
      trigger: { channelId: CANAL_ALERTAS },
    });
  } catch {
    // Notificar é um extra: uma falha aqui nunca atrapalha o alerta nem o SOS.
  }
}

/** Texto curto de cada faixa para a notificação do turno. */
const FAIXA_NA_NOTIFICACAO: Record<FaixaRisco, string> = {
  BAIXO: 'risco baixo',
  MODERADO: 'risco moderado',
  ALTO: 'risco alto',
  MUITO_ALTO: 'risco muito alto',
};

/** Todas as notificações do app, com os textos num lugar só (nenhum dado pessoal). */
export const notificacoes = {
  /** O turno começou, com a faixa do Índice. */
  turnoIniciado: (faixa: FaixaRisco) =>
    notificar('Turno começou', `Seu turno de hoje tem ${FAIXA_NA_NOTIFICACAO[faixa]}. Boa rota!`, { tela: 'turno' }),
  /** O capacete entrou na contagem do alerta de queda. */
  alertaDeQueda: () =>
    notificar('Alerta de queda', 'Toque para cancelar se você está bem.', { tela: 'alerta' }),
  /** O SOS saiu. */
  sosEnviado: (ocorrenciaId: string) =>
    notificar('SOS enviado', 'Seu contato de emergência e a rede de socorro foram avisados.', {
      tela: 'ocorrencia',
      ocorrenciaId,
      papel: 'VITIMA',
    }),
  /** Um voluntário aceitou o SOS da vítima. */
  voluntarioACaminho: (ocorrenciaId: string) =>
    notificar('Socorro a caminho', 'Um voluntário aceitou e está a caminho.', {
      tela: 'ocorrencia',
      ocorrenciaId,
      papel: 'VITIMA',
    }),
  /** A ocorrência foi encerrada (vista pela vítima ou pelo voluntário). */
  ocorrenciaEncerrada: (ocorrenciaId: string, papel: PapelOcorrencia) =>
    notificar('Ocorrência encerrada', 'O atendimento terminou. Toque para ver o resumo.', {
      tela: 'ocorrencia',
      ocorrenciaId,
      papel,
    }),
  /** Um chamado novo apareceu para a conta voluntária disponível. */
  novoChamado: () =>
    notificar('Novo chamado de socorro', 'Alguém caiu perto de você. Toque para ver o chamado.', { tela: 'chamados' }),
  /** O índice de calor medido pelo capacete passou a valer o alerta (uma vez por turno). */
  calorForte: () =>
    notificar('Calor forte agora', 'Pare na sombra e beba água.', { tela: 'turno' }),
};

/** Lê com cuidado o destino guardado nos dados de uma notificação tocada. */
export function destinoDaResposta(resposta: NotificationResponse): DestinoNotificacao | null {
  const destino: unknown = resposta.notification.request.content.data?.destino;
  if (typeof destino !== 'object' || destino === null || !('tela' in destino)) {
    return null;
  }
  const tela: unknown = destino.tela;
  if (tela === 'alerta' || tela === 'chamados' || tela === 'turno') {
    return { tela };
  }
  if (tela === 'ocorrencia' && 'ocorrenciaId' in destino && typeof destino.ocorrenciaId === 'string') {
    const papel = 'papel' in destino && destino.papel === 'VOLUNTARIO' ? 'VOLUNTARIO' : 'VITIMA';
    return { tela, ocorrenciaId: destino.ocorrenciaId, papel };
  }
  return null;
}
