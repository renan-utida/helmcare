import { router, useFocusEffect } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';
import { AccessibilityInfo, BackHandler, ScrollView, Vibration, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { Aviso, type TipoAviso } from '@/componentes/Aviso';
import { Botao } from '@/componentes/Botao';
import { Icone } from '@/componentes/Icone';
import { Texto } from '@/componentes/Texto';
import { CONTAGEM_CANCELAMENTO_S } from '@/config/constantes';
import { useTema } from '@/contextos/TemaContext';
import { ehEstadoDeAlerta, useTurno } from '@/contextos/TurnoContext';
import { useContagemRegressiva } from '@/hooks/useContagemRegressiva';
import { converterErro } from '@/servicos/erros';
import { turnoServico } from '@/servicos/turnoServico';
import { formatarHoraComSegundos } from '@/utils/datas';
import { MENSAGEM_ERRO_GENERICO } from '@/utils/mensagensErro';

/** Tempo que a mensagem final fica na tela antes de ela fechar sozinha. */
const FECHAR_EM_MS = 4000;
/** Padrão de vibração da contagem: 0,6 s vibrando e 0,4 s parado, repetido. */
const VIBRACAO_CONTAGEM = [0, 600, 400];
/** Segundos restantes em que a contagem é anunciada ao leitor de tela, além do início. */
const MARCAS_ANUNCIADAS = [10, 5];

/** Mensagem do resultado do cancelamento ou do fim do alerta. */
interface Mensagem {
  tipo: TipoAviso;
  texto: string;
}

/** Volta para a tela de baixo (ou para a aba Turno, se a tela foi aberta sozinha). */
function fechar() {
  if (router.canGoBack()) {
    router.back();
  } else {
    router.replace('/turno');
  }
}

/**
 * Alerta de queda em tela cheia (seção 7 do contrato, `GET /turnos/{id}/alerta`).
 * O TurnoContext abre esta tela sozinho quando o capacete entra em IMPACTO_DETECTADO,
 * OBSERVACAO ou CONTAGEM, e a troca pela ocorrência quando o SOS sai.
 * - Observação: só informa; o cancelar fica desabilitado e explicado.
 * - Contagem: 15 s no relógio do app a partir da primeira leitura em CONTAGEM, vibração,
 *   anúncios e o botão "Estou bem". Zerou e ainda está em CONTAGEM: "Enviando SOS...".
 *   Quem decide se ainda dá para cancelar é a API (202 ou 409).
 * - Fim (cancelado, falso positivo): mensagem curta e a tela fecha sozinha.
 * Sem gesto de fechar e sem o voltar do Android enquanto o alerta estiver em andamento.
 */
export default function Alerta() {
  const { cores, espaco } = useTema();
  const insets = useSafeAreaInsets();
  const { turno, alerta, alertaSemConexao, contagemVistaEm, atualizarAlerta } = useTurno();
  const [cancelando, setCancelando] = useState(false);
  const [cancelou, setCancelou] = useState(false);
  const [mensagem, setMensagem] = useState<Mensagem | null>(null);

  const estado = alerta?.estadoCapacete ?? null;
  const emVerificacao = estado === 'IMPACTO_DETECTADO' || estado === 'OBSERVACAO';
  const emContagem = estado === 'CONTAGEM';
  const restante = useContagemRegressiva(emContagem ? contagemVistaEm : null, CONTAGEM_CANCELAMENTO_S);
  const enviandoSos = emContagem && restante === 0;
  const sosEnviado = estado === 'SOS_ENVIADO';
  // Terminou: o capacete saiu do alerta sem SOS (cancelado, falso positivo ou de volta ao normal).
  const terminou = alerta !== null && !ehEstadoDeAlerta(estado) && !sosEnviado;
  const bloquearVoltar = !terminou;

  // Sem o botão voltar do Android enquanto o alerta estiver em andamento.
  useFocusEffect(
    useCallback(() => {
      if (!bloquearVoltar) {
        return undefined;
      }
      const assinatura = BackHandler.addEventListener('hardwareBackPress', () => true);
      return () => assinatura.remove();
    }, [bloquearVoltar]),
  );

  // Vibração em padrão repetido durante a contagem; para ao sair dela ou da tela.
  useEffect(() => {
    if (!emContagem) {
      return undefined;
    }
    Vibration.vibrate(VIBRACAO_CONTAGEM, true);
    return () => Vibration.cancel();
  }, [emContagem]);

  // Anúncios: início da contagem, 10 s, 5 s e "Enviando SOS...". Cada um uma vez por contagem.
  const anunciados = useRef(new Set<string>());
  useEffect(() => {
    if (!emContagem || restante === null) {
      anunciados.current.clear();
      return;
    }
    const anunciar = (chave: string, texto: string) => {
      if (!anunciados.current.has(chave)) {
        anunciados.current.add(chave);
        AccessibilityInfo.announceForAccessibility(texto);
      }
    };
    anunciar(
      'inicio',
      `Alerta de queda. ${CONTAGEM_CANCELAMENTO_S} segundos para cancelar. Se você está bem, toque em Estou bem, cancelar alerta.`,
    );
    for (const marca of MARCAS_ANUNCIADAS) {
      if (restante === marca) {
        anunciar(`s${marca}`, `${marca} segundos para cancelar`);
      }
    }
    if (restante === 0) {
      anunciar('sos', 'Enviando SOS');
    }
  }, [emContagem, restante]);

  // Fim do alerta sem SOS: mensagem curta e fecha sozinha em alguns segundos.
  useEffect(() => {
    if (!terminou) {
      return undefined;
    }
    const texto =
      cancelou || estado === 'CANCELADO'
        ? 'Alerta cancelado. Que bom que você está bem.'
        : estado === 'FALSO_POSITIVO'
          ? 'Alarme falso: você voltou a pilotar. Tudo certo.'
          : 'O alerta terminou. Tudo certo.';
    AccessibilityInfo.announceForAccessibility(texto);
    const id = setTimeout(fechar, FECHAR_EM_MS);
    return () => clearTimeout(id);
  }, [terminou, cancelou, estado]);

  // Sem turno (encerrado em outro lugar), não há alerta para mostrar.
  useEffect(() => {
    if (!turno) {
      fechar();
    }
  }, [turno]);

  async function cancelar() {
    if (!turno) {
      return;
    }
    setCancelando(true);
    setMensagem(null);
    try {
      await turnoServico.cancelarAlerta(turno.id);
      // 202: o comando foi para o capacete. A tela fecha quando o estado sair de CONTAGEM.
      setCancelou(true);
      setMensagem({ tipo: 'sucesso', texto: 'Alerta cancelado. Que bom que você está bem.' });
      AccessibilityInfo.announceForAccessibility('Alerta cancelado. Que bom que você está bem.');
      Vibration.cancel();
      // Consulta logo e de novo em seguida, sem esperar os 5 s do polling.
      void atualizarAlerta();
      setTimeout(() => void atualizarAlerta(), 2000);
    } catch (e) {
      const erro = converterErro(e);
      const texto =
        erro.tipo === 'conflito'
          ? 'Não deu para cancelar: o SOS já foi enviado.'
          : erro.tipo === 'indisponivel'
            ? 'O capacete não respondeu. Tente de novo.'
            : erro.tipo === 'sem-conexao'
              ? 'Sem conexão: o cancelamento não chegou. Tente de novo.'
              : MENSAGEM_ERRO_GENERICO;
      setMensagem({ tipo: 'erro', texto });
      AccessibilityInfo.announceForAccessibility(texto);
    } finally {
      setCancelando(false);
    }
  }

  // Cores da tela: as do aviso de erro na contagem e no SOS, e as do aviso de atenção na
  // verificação (pares já conferidos no teste de contraste; o texto sempre diz o estado).
  const corAviso = emContagem || sosEnviado ? cores.aviso.erro : cores.aviso.atencao;
  const fundo = corAviso.fundo;
  const corTexto = corAviso.texto;

  return (
    <ScrollView
      style={{ flex: 1, backgroundColor: terminou ? cores.fundo : fundo }}
      contentContainerStyle={{
        flexGrow: 1,
        justifyContent: 'center',
        gap: espaco.grande,
        paddingTop: insets.top + espaco.grande,
        paddingBottom: insets.bottom + espaco.grande,
        paddingHorizontal: espaco.medio,
      }}
    >
      {alerta === null ? (
        <Texto centralizado accessibilityRole="progressbar" accessibilityLiveRegion="polite">
          Carregando o alerta...
        </Texto>
      ) : terminou ? (
        <View style={{ alignItems: 'center', gap: espaco.medio }}>
          <Icone nome="checkmark-circle" cor={cores.aviso.sucesso.icone} tamanho={72} />
          <Texto variante="titulo" centralizado accessibilityLiveRegion="polite">
            {cancelou || estado === 'CANCELADO'
              ? 'Alerta cancelado. Que bom que você está bem.'
              : estado === 'FALSO_POSITIVO'
                ? 'Alarme falso: você voltou a pilotar. Tudo certo.'
                : 'O alerta terminou. Tudo certo.'}
          </Texto>
          <Texto cor="textoSecundario" centralizado>
            Esta tela fecha sozinha.
          </Texto>
        </View>
      ) : (
        <>
          <View style={{ alignItems: 'center', gap: espaco.medio }}>
            <Icone nome={emContagem ? 'timer' : sosEnviado ? 'medkit' : 'alert-circle'} cor={corAviso.icone} tamanho={72} />
            <Texto variante="titulo" centralizado corPersonalizada={corTexto}>
              {sosEnviado ? 'SOS enviado' : 'Alerta de queda'}
            </Texto>
          </View>

          {emVerificacao ? (
            <Texto variante="subtitulo" centralizado corPersonalizada={corTexto} accessibilityLiveRegion="polite">
              Detectamos um impacto. Estamos verificando se você está bem.
            </Texto>
          ) : null}

          {emContagem && restante !== null ? (
            <View style={{ alignItems: 'center', gap: espaco.pequeno }}>
              {enviandoSos ? (
                <Texto variante="titulo" centralizado corPersonalizada={corTexto} accessibilityLiveRegion="assertive">
                  Enviando SOS...
                </Texto>
              ) : (
                <>
                  <Texto
                    variante="contagem"
                    corPersonalizada={corTexto}
                    centralizado
                    // Sem região viva: ela fazia o TalkBack ler todos os segundos e cortar os
                    // anúncios. O número é lido quando a pessoa toca nele.
                    accessibilityLabel={`${restante} segundos para cancelar`}
                  >
                    {restante}
                  </Texto>
                  <Texto variante="subtitulo" centralizado corPersonalizada={corTexto}>
                    segundos para cancelar
                  </Texto>
                </>
              )}
              {alerta.contagemTerminaEm ? (
                <Texto variante="legenda" centralizado corPersonalizada={corTexto}>
                  {`No relógio do capacete, a contagem termina às ${formatarHoraComSegundos(alerta.contagemTerminaEm)}.`}
                </Texto>
              ) : null}
            </View>
          ) : null}

          {sosEnviado ? (
            <Texto variante="subtitulo" centralizado corPersonalizada={corTexto} accessibilityLiveRegion="assertive">
              Pedido de socorro enviado. Abrindo a ocorrência...
            </Texto>
          ) : null}

          {alertaSemConexao ? (
            <Aviso tipo="atencao" mensagem="Sem conexão: o estado do alerta pode estar desatualizado." />
          ) : null}
          {mensagem ? <Aviso tipo={mensagem.tipo} mensagem={mensagem.texto} /> : null}

          {!sosEnviado ? (
            <View style={{ gap: espaco.pequeno }}>
              <Botao
                titulo="Estou bem, cancelar alerta"
                icone="hand-left"
                alturaMinima={72}
                aoPressionar={cancelar}
                carregando={cancelando}
                // Habilitado enquanto o estado for CONTAGEM, mesmo depois de um 202: se o comando
                // se perder, dá para tentar de novo. Quem decide é a API (202 ou 409).
                desabilitado={!emContagem}
                accessibilityHint={
                  emContagem
                    ? 'Cancela o pedido de socorro. Toque só se você está bem.'
                    : 'Você poderá cancelar quando a contagem começar'
                }
              />
              {emVerificacao ? (
                <Texto variante="legenda" centralizado corPersonalizada={corTexto}>
                  Você poderá cancelar quando a contagem começar.
                </Texto>
              ) : null}
            </View>
          ) : null}
        </>
      )}
    </ScrollView>
  );
}
