import { router, useLocalSearchParams } from 'expo-router';
import { useEffect, useRef, useState } from 'react';
import { AccessibilityInfo, Alert, Linking, View } from 'react-native';

import type { PapelOcorrencia } from '@/banco/ocorrenciaRepositorio';
import { Aviso, type TipoAviso } from '@/componentes/Aviso';
import { Botao } from '@/componentes/Botao';
import { Carregando } from '@/componentes/Carregando';
import { Cartao } from '@/componentes/Cartao';
import { EstadoErro } from '@/componentes/EstadoErro';
import { EstadoVazio } from '@/componentes/EstadoVazio';
import { Icone, type NomeIcone } from '@/componentes/Icone';
import { Tela } from '@/componentes/Tela';
import { Texto } from '@/componentes/Texto';
import { RAIO_CAMADA_1_KM } from '@/config/constantes';
import { useTema } from '@/contextos/TemaContext';
import { useCronometro } from '@/hooks/useCronometro';
import { ESTADOS_FINAIS, useOcorrencia } from '@/hooks/useOcorrencia';
import { converterErro } from '@/servicos/erros';
import { ocorrenciaServico } from '@/servicos/ocorrenciaServico';
import type { EstadoOcorrencia } from '@/tipos/contrato';
import { formatarMinutosSegundos } from '@/utils/datas';
import { MENSAGEM_SEM_CONEXAO, mensagemComum } from '@/utils/mensagensErro';
import { repetirEmConflito } from '@/utils/repetirEmConflito';

/** Status de cada estado da ocorrência em linguagem simples, com ícone. */
const STATUS: Record<EstadoOcorrencia, { texto: string; icone: NomeIcone }> = {
  SOS_CAMADA_1: { texto: `Avisando voluntários a até ${RAIO_CAMADA_1_KM} km`, icone: 'radio' },
  SOS_CAMADA_2: { texto: 'Avisando toda a rede de voluntários', icone: 'megaphone' },
  SOCORRO_A_CAMINHO: { texto: 'Um voluntário aceitou e está a caminho', icone: 'walk' },
  ENCERRADA: { texto: 'Ocorrência encerrada', icone: 'checkmark-circle' },
  CANCELADA: { texto: 'SOS cancelado', icone: 'close-circle' },
};

/** Estados em que a vítima ainda pode dizer que está bem (antes do aceite). */
const ESTADOS_CANCELAVEIS: readonly EstadoOcorrencia[] = ['SOS_CAMADA_1', 'SOS_CAMADA_2'];

/** Lê o papel da rota; qualquer valor diferente de VOLUNTARIO é tratado como vítima. */
function papelDaRota(valor: string | string[] | undefined): PapelOcorrencia {
  return valor === 'VOLUNTARIO' ? 'VOLUNTARIO' : 'VITIMA';
}

/** Duração falada, como "1 minuto e 5 segundos". */
function duracaoFalada(totalSegundos: number): string {
  const minutos = Math.floor(totalSegundos / 60);
  const segundos = totalSegundos % 60;
  const partes = [];
  if (minutos > 0) partes.push(`${minutos} ${minutos === 1 ? 'minuto' : 'minutos'}`);
  partes.push(`${segundos} ${segundos === 1 ? 'segundo' : 'segundos'}`);
  return partes.join(' e ');
}

/**
 * Ocorrência (o SOS), aberta pelo alerta ou pela aba Turno. Parâmetro `papel`: VITIMA nesta
 * entrega; a próxima acrescenta VOLUNTARIO nesta mesma tela. Mostra o status do socorro,
 * o cronômetro com a meta de 2 minutos, o "Ligar 192" e o cancelamento pela vítima.
 */
export default function Ocorrencia() {
  const parametros = useLocalSearchParams<{ id: string; papel?: string }>();
  const papel = papelDaRota(parametros.papel);
  const id = parametros.id;

  if (papel === 'VOLUNTARIO') {
    return (
      <Tela>
        <EstadoVazio titulo="Disponível em breve" mensagem="A visão do voluntário chega na próxima versão." />
      </Tela>
    );
  }
  return <OcorrenciaDaVitima id={id} />;
}

/** Mensagem mostrada depois de uma ação (cancelar) ou de um erro. */
interface MensagemTela {
  tipo: TipoAviso;
  texto: string;
}

/** Tela da vítima: status, cronômetro, Ligar 192 e "Estou bem, cancelar o SOS". */
function OcorrenciaDaVitima({ id }: { id: string }) {
  const { cores, espaco } = useTema();
  const { ocorrencia, lidoEm, erro, recarregar, aplicar } = useOcorrencia(id, 'VITIMA');
  const segundos = useCronometro(ocorrencia?.cronometro ?? null, lidoEm);
  const [cancelando, setCancelando] = useState(false);
  const [mensagem, setMensagem] = useState<MensagemTela | null>(null);

  // Anuncia cada mudança de estado (por exemplo, "Um voluntário aceitou e está a caminho").
  const estadoAnunciado = useRef<EstadoOcorrencia | null>(null);
  useEffect(() => {
    const estado = ocorrencia?.estado;
    if (estado && estado !== estadoAnunciado.current) {
      estadoAnunciado.current = estado;
      AccessibilityInfo.announceForAccessibility(STATUS[estado].texto);
    }
  }, [ocorrencia?.estado]);

  if (!ocorrencia) {
    if (erro) {
      const texto =
        erro.tipo === 'nao-encontrado'
          ? 'Ocorrência não encontrada.'
          : erro.tipo === 'sem-conexao'
            ? MENSAGEM_SEM_CONEXAO
            : mensagemComum(erro);
      return (
        <Tela>
          <EstadoErro mensagem={texto} aoTentarDeNovo={() => void recarregar()} />
          <Botao titulo="Voltar ao turno" variante="secundaria" aoPressionar={() => router.dismissTo('/turno')} />
        </Tela>
      );
    }
    return <Carregando mensagem="Abrindo a ocorrência..." />;
  }

  const ativa = !ESTADOS_FINAIS.includes(ocorrencia.estado);
  const status = STATUS[ocorrencia.estado];
  const meta = ocorrencia.cronometro.metaSegundos;
  // Situação da meta: ativa compara o cronômetro; terminada usa o tempo até o aceite
  // (o indicador do contrato) ou, sem aceite, não compara com a meta.
  const aceiteFinal = ocorrencia.tempoAteAceiteSegundos;
  const situacaoMeta: 'dentro' | 'acima' | 'sem-aceite' = ativa
    ? segundos !== null && segundos <= meta
      ? 'dentro'
      : 'acima'
    : aceiteFinal === null
      ? 'sem-aceite'
      : aceiteFinal <= meta
        ? 'dentro'
        : 'acima';
  const TEXTO_META = { dentro: 'Dentro da meta', acima: 'Acima da meta', 'sem-aceite': 'Sem aceite de voluntário' };

  function ligar192() {
    // Abre o discador com o número; a ligação só começa se a pessoa tocar em ligar.
    void Linking.openURL('tel:192');
  }

  async function cancelarSos() {
    setCancelando(true);
    setMensagem(null);
    try {
      // 409 pode ser conflito com o escalonamento da camada 2: repete até 3 vezes.
      const resposta = await repetirEmConflito(() => ocorrenciaServico.cancelar(id));
      await aplicar(resposta);
      setMensagem({ tipo: 'sucesso', texto: 'SOS cancelado. Que bom que você está bem.' });
    } catch (e) {
      const erroApi = converterErro(e);
      if (erroApi.tipo === 'conflito') {
        // Continuou dando 409: relê a ocorrência e mostra o estado atual.
        await recarregar();
        setMensagem({ tipo: 'atencao', texto: 'Não deu para cancelar: a ocorrência mudou. Veja o estado atual acima.' });
      } else if (erroApi.tipo === 'sem-conexao') {
        setMensagem({ tipo: 'erro', texto: `O SOS continua ativo. ${MENSAGEM_SEM_CONEXAO}` });
      } else {
        setMensagem({ tipo: 'erro', texto: mensagemComum(erroApi) });
      }
    } finally {
      setCancelando(false);
    }
  }

  function confirmarCancelamento() {
    Alert.alert('Cancelar o SOS?', 'Os voluntários deixam de ser chamados. Cancele só se você está bem.', [
      { text: 'Manter o SOS', style: 'cancel' },
      { text: 'Estou bem, cancelar', style: 'destructive', onPress: () => void cancelarSos() },
    ]);
  }

  return (
    <Tela aoAtualizar={() => void recarregar()} atualizando={false}>
      <View
        accessible
        accessibilityLabel={`Status do socorro: ${status.texto}`}
        style={{ flexDirection: 'row', alignItems: 'center', gap: espaco.medio }}
      >
        <Icone nome={status.icone} cor={ativa ? cores.aviso.erro.icone : cores.aviso.sucesso.icone} tamanho={40} />
        <Texto variante="titulo" style={{ flex: 1 }}>
          {status.texto}
        </Texto>
      </View>

      <Aviso tipo="informacao" mensagem="Seu contato de emergência foi avisado." />
      {erro?.tipo === 'sem-conexao' ? (
        <Aviso tipo="atencao" mensagem="Sem conexão: o status pode estar desatualizado." />
      ) : null}

      <Cartao>
        <View
          accessible
          accessibilityLabel={
            segundos === null
              ? 'Cronômetro indisponível'
              : `${duracaoFalada(segundos)} desde a queda. Meta: socorro em até ${meta / 60} minutos. ${TEXTO_META[situacaoMeta]}.`
          }
          style={{ gap: espaco.pequeno }}
        >
          <Texto variante="destaque">{segundos === null ? '--:--' : formatarMinutosSegundos(segundos)}</Texto>
          <Texto variante="rotulo">desde a queda</Texto>
          <Texto>{`Meta: socorro em até ${meta / 60} minutos`}</Texto>
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: espaco.pequeno }}>
            <Icone
              nome={situacaoMeta === 'dentro' ? 'checkmark-circle' : situacaoMeta === 'acima' ? 'time' : 'remove-circle'}
              cor={situacaoMeta === 'dentro' ? cores.aviso.sucesso.icone : cores.aviso.atencao.icone}
            />
            <Texto variante="rotulo">{TEXTO_META[situacaoMeta]}</Texto>
          </View>
          {ocorrencia.cronometro.parado ? <Texto variante="legenda">O cronômetro parou.</Texto> : null}
        </View>
      </Cartao>

      {ativa ? (
        <Botao
          titulo="Ligar 192 (SAMU)"
          variante="perigo"
          icone="call"
          alturaMinima={72}
          aoPressionar={ligar192}
          accessibilityHint="Abre o discador com o número 192. A ligação só começa se você tocar em ligar."
        />
      ) : null}

      {mensagem ? <Aviso tipo={mensagem.tipo} mensagem={mensagem.texto} /> : null}

      {ESTADOS_CANCELAVEIS.includes(ocorrencia.estado) ? (
        <Botao
          titulo="Estou bem, cancelar o SOS"
          variante="secundaria"
          icone="hand-left"
          aoPressionar={confirmarCancelamento}
          carregando={cancelando}
          accessibilityHint="Pede confirmação antes de cancelar o pedido de socorro"
        />
      ) : null}

      {!ativa ? (
        <Cartao titulo="Resumo">
          <Texto>
            {ocorrencia.tempoAteAceiteSegundos === null
              ? 'Tempo até o aceite: sem aceite.'
              : `Tempo até o aceite: ${formatarMinutosSegundos(ocorrencia.tempoAteAceiteSegundos)} (${duracaoFalada(ocorrencia.tempoAteAceiteSegundos)}).`}
          </Texto>
          <Botao titulo="Voltar ao turno" icone="arrow-back" aoPressionar={() => router.dismissTo('/turno')} />
        </Cartao>
      ) : null}
    </Tela>
  );
}
