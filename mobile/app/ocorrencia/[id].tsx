import { router, useLocalSearchParams } from 'expo-router';
import { useEffect, useRef, useState, type RefObject } from 'react';
import { AccessibilityInfo, Alert, Linking, Vibration, View } from 'react-native';

import type { PapelOcorrencia } from '@/banco/ocorrenciaRepositorio';
import { Aviso, type TipoAviso } from '@/componentes/Aviso';
import { Botao } from '@/componentes/Botao';
import { BotaoLigar192 } from '@/componentes/BotaoLigar192';
import { Carregando } from '@/componentes/Carregando';
import { Cartao } from '@/componentes/Cartao';
import { CartaoCronometro, situacaoDaMeta, TEXTO_META } from '@/componentes/CartaoCronometro';
import { EstadoErro } from '@/componentes/EstadoErro';
import { FaixaSessaoTerminada } from '@/componentes/FaixaSessaoTerminada';
import { Icone, type NomeIcone } from '@/componentes/Icone';
import { Tela } from '@/componentes/Tela';
import { Texto } from '@/componentes/Texto';
import { RAIO_CAMADA_1_KM, TEMPO_ENCERRAMENTO_AUTOMATICO_MIN } from '@/config/constantes';
import { useChamados } from '@/contextos/ChamadosContext';
import { useTema } from '@/contextos/TemaContext';
import { useCronometro } from '@/hooks/useCronometro';
import { ESTADOS_FINAIS, useOcorrencia } from '@/hooks/useOcorrencia';
import { converterErro, type ErroApi } from '@/servicos/erros';
import { notificacoes } from '@/servicos/notificacoes';
import { ocorrenciaServico } from '@/servicos/ocorrenciaServico';
import type { EstadoOcorrencia, OcorrenciaResposta } from '@/tipos/contrato';
import { duracaoFalada, formatarDecimal, formatarMinutosSegundos, textoEncerramentoAutomatico } from '@/utils/datas';
import { MENSAGEM_SEM_CONEXAO, mensagemComum } from '@/utils/mensagensErro';
import { repetirEmConflito } from '@/utils/repetirEmConflito';

/** Texto e ícone de um estado da ocorrência. */
interface StatusTela {
  texto: string;
  icone: NomeIcone;
}

/** Status de cada estado em linguagem simples, como a vítima vê. */
const STATUS_VITIMA: Record<EstadoOcorrencia, StatusTela> = {
  SOS_CAMADA_1: { texto: `Avisando voluntários a até ${RAIO_CAMADA_1_KM} km`, icone: 'radio' },
  SOS_CAMADA_2: { texto: 'Avisando toda a rede de voluntários', icone: 'megaphone' },
  SOCORRO_A_CAMINHO: { texto: 'Um voluntário aceitou e está a caminho', icone: 'walk' },
  ENCERRADA: { texto: 'Ocorrência encerrada', icone: 'checkmark-circle' },
  CANCELADA: { texto: 'SOS cancelado', icone: 'close-circle' },
};

/** Status de cada estado em linguagem simples, como o voluntário vê. */
const STATUS_VOLUNTARIO: Record<EstadoOcorrencia, StatusTela> = {
  SOS_CAMADA_1: { texto: 'Chamado aguardando um voluntário', icone: 'radio' },
  SOS_CAMADA_2: { texto: 'Chamado aguardando um voluntário', icone: 'megaphone' },
  SOCORRO_A_CAMINHO: { texto: 'Você aceitou. Vá até o entregador.', icone: 'walk' },
  ENCERRADA: { texto: 'Atendimento encerrado', icone: 'checkmark-circle' },
  CANCELADA: { texto: 'O entregador cancelou o SOS: ele está bem', icone: 'close-circle' },
};

/** Vibração curta para a vítima quando um voluntário aceita (quem caiu pode não estar olhando a tela). */
const VIBRACAO_SOCORRO_A_CAMINHO = [0, 300, 150, 300];

/** Estados em que a vítima ainda pode dizer que está bem (antes do aceite). */
const ESTADOS_CANCELAVEIS: readonly EstadoOcorrencia[] = ['SOS_CAMADA_1', 'SOS_CAMADA_2'];

/** Lê o papel da rota; qualquer valor diferente de VOLUNTARIO é tratado como vítima. */
function papelDaRota(valor: string | string[] | undefined): PapelOcorrencia {
  return valor === 'VOLUNTARIO' ? 'VOLUNTARIO' : 'VITIMA';
}

/** Mensagem mostrada depois de uma ação (cancelar, encerrar, abrir o mapa) ou de um erro. */
interface MensagemTela {
  tipo: TipoAviso;
  texto: string;
}

/**
 * Ocorrência (o SOS). Parâmetro `papel`: VITIMA (aberta pelo alerta, pela Início ou pelo turno) ou
 * VOLUNTARIO (aberta pelo aceite na aba Chamados). As duas mostram o status do socorro, o
 * cronômetro com a meta de 2 minutos e o "Ligar 192"; a vítima pode cancelar o SOS e o
 * voluntário vê a localização exata e encerra o atendimento.
 */
export default function Ocorrencia() {
  const parametros = useLocalSearchParams<{ id: string; papel?: string }>();
  const papel = papelDaRota(parametros.papel);
  const id = parametros.id;
  return papel === 'VOLUNTARIO' ? <OcorrenciaDoVoluntario id={id} /> : <OcorrenciaDaVitima id={id} />;
}

/**
 * Anuncia cada mudança de estado com o texto do papel (por exemplo, "Um voluntário aceitou e
 * está a caminho" para a vítima e "Atendimento encerrado" para o voluntário). Com
 * `vibrarNoAceite`, também vibra quando o estado passa a SOCORRO_A_CAMINHO.
 */
function useAnunciarEstado(
  estado: EstadoOcorrencia | undefined,
  status: Record<EstadoOcorrencia, StatusTela>,
  opcoes: {
    ocorrenciaId: string;
    papel: PapelOcorrencia;
    vibrarNoAceite?: boolean;
    /** Verdadeiro enquanto a própria pessoa encerra ou cancela: o fim dela não vira notificação. */
    acaoPropria: RefObject<boolean>;
  },
) {
  const { ocorrenciaId, papel, vibrarNoAceite = false, acaoPropria } = opcoes;
  const anunciado = useRef<EstadoOcorrencia | null>(null);
  useEffect(() => {
    if (estado && estado !== anunciado.current) {
      // Só as MUDANÇAS viram notificação; a primeira leitura (ao abrir a tela) não.
      const mudou = anunciado.current !== null;
      anunciado.current = estado;
      AccessibilityInfo.announceForAccessibility(status[estado].texto);
      if (vibrarNoAceite && estado === 'SOCORRO_A_CAMINHO') {
        Vibration.vibrate(VIBRACAO_SOCORRO_A_CAMINHO);
      }
      if (mudou && papel === 'VITIMA' && estado === 'SOCORRO_A_CAMINHO') {
        void notificacoes.voluntarioACaminho(ocorrenciaId);
      }
      if (mudou && ESTADOS_FINAIS.includes(estado) && !acaoPropria.current) {
        void notificacoes.ocorrenciaEncerrada(ocorrenciaId, papel);
      }
    }
  }, [estado, status, vibrarNoAceite, ocorrenciaId, papel, acaoPropria]);
}

/** Tela de erro ou de carregamento enquanto a primeira leitura não chega. */
function SemOcorrencia({
  erro,
  aoTentarDeNovo,
  voltar,
}: {
  erro: ErroApi | null;
  aoTentarDeNovo: () => void;
  voltar: { titulo: string; destino: '/inicio' | '/chamados' };
}) {
  if (!erro) {
    return <Carregando mensagem="Abrindo a ocorrência..." />;
  }
  const texto =
    erro.tipo === 'nao-encontrado'
      ? 'Ocorrência não encontrada.'
      : erro.tipo === 'sem-conexao'
        ? MENSAGEM_SEM_CONEXAO
        : mensagemComum(erro);
  return (
    <Tela>
      <EstadoErro mensagem={texto} aoTentarDeNovo={aoTentarDeNovo} />
      <Botao titulo={voltar.titulo} variante="secundaria" aoPressionar={() => router.dismissTo(voltar.destino)} />
    </Tela>
  );
}

/** Cabeçalho com o status do socorro: ícone e frase, lidos juntos. */
function CabecalhoStatus({ status, ativa }: { status: StatusTela; ativa: boolean }) {
  const { cores, espaco } = useTema();
  return (
    <View
      accessible
      accessibilityRole="header"
      accessibilityLabel={`Status do socorro: ${status.texto}`}
      style={{ flexDirection: 'row', alignItems: 'center', gap: espaco.medio }}
    >
      <Icone nome={status.icone} cor={ativa ? cores.aviso.erro.icone : cores.aviso.sucesso.icone} tamanho={40} />
      <Texto variante="titulo" style={{ flex: 1 }}>
        {status.texto}
      </Texto>
    </View>
  );
}

/** Frase do resumo com o tempo até o aceite e a situação da meta. */
function fraseResumo(ocorrencia: OcorrenciaResposta): string {
  // A API encerrou o SOS por tempo (`encerradaAutomaticamente`, seção 7).
  const automatica = ocorrencia.encerradaAutomaticamente
    ? ` ${textoEncerramentoAutomatico(TEMPO_ENCERRAMENTO_AUTOMATICO_MIN)}.`
    : '';
  const tempo = ocorrencia.tempoAteAceiteSegundos;
  if (tempo === null) {
    return `Tempo até o aceite: sem aceite.${automatica}`;
  }
  const meta = TEXTO_META[situacaoDaMeta(tempo, ocorrencia.cronometro.metaSegundos)].toLowerCase();
  return `Tempo até o aceite: ${formatarMinutosSegundos(tempo)} (${duracaoFalada(tempo)}), ${meta}.${automatica}`;
}

/**
 * Distância do voluntário que aceitou, para a vítima: "Voluntário a 0,6 km" (1 casa, vírgula),
 * atualizada a cada consulta e anunciada uma vez quando aparece. Só a distância: a posição do
 * voluntário nunca sai da API (seção 9). Sem `distanciaVoluntarioKm`, nada aparece.
 */
function DistanciaVoluntario({ km }: { km: number | null }) {
  const { cores, espaco } = useTema();
  const anunciada = useRef(false);
  useEffect(() => {
    if (km !== null && !anunciada.current) {
      anunciada.current = true;
      AccessibilityInfo.announceForAccessibility(`Voluntário a ${formatarDecimal(km)} quilômetros`);
    }
  }, [km]);
  if (km === null) {
    return null;
  }
  return (
    <View
      accessible
      accessibilityLabel={`Voluntário a ${formatarDecimal(km)} quilômetros`}
      style={{ flexDirection: 'row', alignItems: 'center', gap: espaco.pequeno }}
    >
      <Icone nome="walk" cor={cores.aviso.sucesso.icone} tamanho={32} />
      <Texto variante="subtitulo" style={{ flex: 1 }}>{`Voluntário a ${formatarDecimal(km)} km`}</Texto>
    </View>
  );
}

/**
 * Tela da vítima: status, cronômetro, Ligar 192, "Estou bem, cancelar o SOS" (antes do aceite) e
 * "Já fui atendido, encerrar" (depois do aceite, por exemplo quando o SAMU chegou: o contrato
 * deixa a vítima encerrar, com ou sem aceite).
 */
function OcorrenciaDaVitima({ id }: { id: string }) {
  const { ocorrencia, lidoEm, erro, recarregar, aplicar } = useOcorrencia(id, 'VITIMA');
  const segundos = useCronometro(ocorrencia?.cronometro ?? null, lidoEm);
  const [cancelando, setCancelando] = useState(false);
  const [encerrando, setEncerrando] = useState(false);
  const [mensagem, setMensagem] = useState<MensagemTela | null>(null);
  const acaoPropria = useRef(false);
  useAnunciarEstado(ocorrencia?.estado, STATUS_VITIMA, {
    ocorrenciaId: id,
    papel: 'VITIMA',
    vibrarNoAceite: true,
    acaoPropria,
  });

  if (!ocorrencia) {
    return (
      <SemOcorrencia
        erro={erro}
        aoTentarDeNovo={() => void recarregar()}
        voltar={{ titulo: 'Voltar ao início', destino: '/inicio' }}
      />
    );
  }

  const ativa = !ESTADOS_FINAIS.includes(ocorrencia.estado);

  async function cancelarSos() {
    setCancelando(true);
    setMensagem(null);
    try {
      // 409 pode ser conflito com o escalonamento da camada 2: repete até 3 vezes.
      const resposta = await repetirEmConflito(() => ocorrenciaServico.cancelar(id));
      acaoPropria.current = true;
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

  /** `POST /ocorrencias/{id}/encerrar` pela vítima, depois do aceite (409: repete até 3 vezes). */
  async function encerrarAtendido() {
    setEncerrando(true);
    setMensagem(null);
    try {
      const resposta = await repetirEmConflito(() => ocorrenciaServico.encerrar(id));
      acaoPropria.current = true;
      await aplicar(resposta);
      setMensagem({ tipo: 'sucesso', texto: 'Ocorrência encerrada. Melhoras!' });
    } catch (e) {
      const erroApi = converterErro(e);
      if (erroApi.tipo === 'conflito') {
        await recarregar();
        setMensagem({ tipo: 'atencao', texto: 'Não deu para encerrar: a ocorrência mudou. Veja o estado atual acima.' });
      } else if (erroApi.tipo === 'sem-conexao') {
        setMensagem({ tipo: 'erro', texto: `A ocorrência continua aberta. ${MENSAGEM_SEM_CONEXAO}` });
      } else {
        setMensagem({ tipo: 'erro', texto: mensagemComum(erroApi) });
      }
    } finally {
      setEncerrando(false);
    }
  }

  function confirmarEncerramento() {
    Alert.alert('Encerrar a ocorrência?', 'O voluntário deixa de ser esperado.', [
      { text: 'Continuar esperando', style: 'cancel' },
      { text: 'Já fui atendido, encerrar', style: 'destructive', onPress: () => void encerrarAtendido() },
    ]);
  }

  return (
    // A faixa do fim da sessão fica fixa em cima, fora da rolagem; o 192 continua na tela.
    <View style={{ flex: 1 }}>
      <FaixaSessaoTerminada />
      <Tela aoAtualizar={() => void recarregar()} atualizando={false}>
        <CabecalhoStatus status={STATUS_VITIMA[ocorrencia.estado]} ativa={ativa} />

        {ocorrencia.estado === 'SOCORRO_A_CAMINHO' ? <DistanciaVoluntario km={ocorrencia.distanciaVoluntarioKm} /> : null}

        <Aviso tipo="informacao" mensagem="Seu contato de emergência foi avisado." />
        {erro?.tipo === 'sem-conexao' ? (
          <Aviso tipo="atencao" mensagem="Sem conexão: o status pode estar desatualizado." />
        ) : null}

        <CartaoCronometro ocorrencia={ocorrencia} segundos={segundos} papel="VITIMA" />

        {ativa ? <BotaoLigar192 /> : null}

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

        {ocorrencia.estado === 'SOCORRO_A_CAMINHO' ? (
          <Botao
            titulo="Já fui atendido, encerrar"
            variante="secundaria"
            icone="checkmark-done"
            aoPressionar={confirmarEncerramento}
            carregando={encerrando}
            accessibilityHint="Pede confirmação antes de encerrar. Use quando o SAMU ou outra ajuda já atendeu você."
          />
        ) : null}

        {!ativa ? (
          <Cartao titulo="Resumo">
            <Texto>{fraseResumo(ocorrencia)}</Texto>
            <Botao titulo="Voltar ao início" icone="arrow-back" aoPressionar={() => router.dismissTo('/inicio')} />
          </Cartao>
        ) : null}
      </Tela>
    </View>
  );
}

/**
 * Tela do voluntário: `GET /ocorrencias/{id}?voluntarioId=` a cada 5 s. Com `localizacaoExata`
 * verdadeiro, "Localização exata liberada" e o botão "Abrir no mapa"; sem ela, só a distância.
 * As coordenadas ficam só na memória da tela (nunca no SQLite nem em log) e só saem para o app
 * de mapas. O cronômetro para no aceite; "Encerrar atendimento" pede confirmação.
 */
function OcorrenciaDoVoluntario({ id }: { id: string }) {
  const { cores, espaco } = useTema();
  const { ocorrencia, lidoEm, erro, recarregar, aplicar } = useOcorrencia(id, 'VOLUNTARIO');
  const { distanciaDe, atualizarOcorrenciaAceita } = useChamados();
  const segundos = useCronometro(ocorrencia?.cronometro ?? null, lidoEm);
  const [encerrando, setEncerrando] = useState(false);
  const [mensagem, setMensagem] = useState<MensagemTela | null>(null);
  const acaoPropria = useRef(false);
  useAnunciarEstado(ocorrencia?.estado, STATUS_VOLUNTARIO, { ocorrenciaId: id, papel: 'VOLUNTARIO', acaoPropria });

  // Quando a ocorrência termina (encerrada aqui, ou cancelada pela vítima), a aba Chamados
  // deixa de mostrar o "Atendimento em andamento": relê o SQLite, que o useOcorrencia já atualizou.
  const terminou = ocorrencia !== null && ESTADOS_FINAIS.includes(ocorrencia.estado);
  useEffect(() => {
    if (terminou) {
      void atualizarOcorrenciaAceita();
    }
  }, [terminou, atualizarOcorrenciaAceita]);

  if (!ocorrencia) {
    return (
      <SemOcorrencia
        erro={erro}
        aoTentarDeNovo={() => void recarregar()}
        voltar={{ titulo: 'Voltar aos chamados', destino: '/chamados' }}
      />
    );
  }

  const ativa = !terminou;
  const distancia = distanciaDe(id);

  async function abrirMapa(atual: OcorrenciaResposta) {
    // Formato geo do Android: abre o app de mapas com um alfinete e o nome do ponto.
    const ponto = `${atual.latitude},${atual.longitude}`;
    const url = `geo:${ponto}?q=${ponto}(${encodeURIComponent('Entregador caído')})`;
    try {
      await Linking.openURL(url);
    } catch {
      // Sem app de mapas no celular. Nada vai para log: a URL tem a localização exata.
      setMensagem({ tipo: 'erro', texto: 'Não deu para abrir o mapa neste celular.' });
    }
  }

  async function encerrar() {
    setEncerrando(true);
    setMensagem(null);
    try {
      // 409 pode ser conflito com o escalonamento: repete até 3 vezes.
      const resposta = await repetirEmConflito(() => ocorrenciaServico.encerrar(id));
      acaoPropria.current = true;
      await aplicar(resposta);
      setMensagem({ tipo: 'sucesso', texto: 'Atendimento encerrado. Obrigado por ajudar.' });
    } catch (e) {
      const erroApi = converterErro(e);
      if (erroApi.tipo === 'conflito') {
        await recarregar();
        setMensagem({ tipo: 'atencao', texto: 'Não deu para encerrar: a ocorrência mudou. Veja o estado atual acima.' });
      } else if (erroApi.tipo === 'sem-conexao') {
        setMensagem({ tipo: 'erro', texto: `O atendimento continua aberto. ${MENSAGEM_SEM_CONEXAO}` });
      } else if (erroApi.tipo === 'nao-encontrado') {
        setMensagem({ tipo: 'erro', texto: 'Ocorrência não encontrada.' });
      } else {
        setMensagem({ tipo: 'erro', texto: mensagemComum(erroApi) });
      }
    } finally {
      setEncerrando(false);
    }
  }

  function confirmarEncerrar() {
    Alert.alert(
      'Encerrar o atendimento?',
      'Encerre quando o entregador estiver com o SAMU ou não precisar mais de ajuda.',
      [
        { text: 'Continuar atendendo', style: 'cancel' },
        { text: 'Encerrar', style: 'destructive', onPress: () => void encerrar() },
      ],
    );
  }

  return (
    // A faixa do fim da sessão fica fixa em cima, fora da rolagem; o 192 continua na tela.
    <View style={{ flex: 1 }}>
      <FaixaSessaoTerminada />
      <Tela aoAtualizar={() => void recarregar()} atualizando={false}>
        <CabecalhoStatus status={STATUS_VOLUNTARIO[ocorrencia.estado]} ativa={ativa} />

        {erro?.tipo === 'sem-conexao' ? (
          <Aviso tipo="atencao" mensagem="Sem conexão: o status pode estar desatualizado." />
        ) : null}

        {ativa ? (
          <Cartao titulo="Onde está o entregador">
            {ocorrencia.localizacaoExata ? (
              <>
                <View
                  accessible
                  accessibilityLabel="Localização exata liberada"
                  style={{ flexDirection: 'row', alignItems: 'center', gap: espaco.pequeno }}
                >
                  <Icone nome="location" cor={cores.aviso.sucesso.icone} />
                  <Texto variante="rotulo" style={{ flex: 1 }}>
                    Localização exata liberada
                  </Texto>
                </View>
                <Botao
                  titulo="Abrir no mapa"
                  icone="map"
                  aoPressionar={() => void abrirMapa(ocorrencia)}
                  accessibilityHint="Abre o app de mapas com o ponto onde o entregador caiu"
                />
              </>
            ) : (
              <Texto>A localização exata aparece aqui depois do seu aceite.</Texto>
            )}
            {distancia !== null ? (
              <Texto cor="textoSecundario">{`Cerca de ${formatarDecimal(distancia)} km de você (distância aproximada).`}</Texto>
            ) : null}
          </Cartao>
        ) : null}

        <CartaoCronometro ocorrencia={ocorrencia} segundos={segundos} papel="VOLUNTARIO" />

        {ativa ? <BotaoLigar192 /> : null}

        {mensagem ? <Aviso tipo={mensagem.tipo} mensagem={mensagem.texto} /> : null}

        {ativa ? (
          <Botao
            titulo="Encerrar atendimento"
            variante="secundaria"
            icone="checkmark-done"
            aoPressionar={confirmarEncerrar}
            carregando={encerrando}
            accessibilityHint="Pede confirmação antes de encerrar"
          />
        ) : (
          <Cartao titulo="Resumo">
            <Texto>{fraseResumo(ocorrencia)}</Texto>
            <Botao titulo="Voltar aos chamados" icone="arrow-back" aoPressionar={() => router.dismissTo('/chamados')} />
          </Cartao>
        )}
      </Tela>
    </View>
  );
}
