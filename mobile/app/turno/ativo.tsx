import { router, useFocusEffect } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';
import { AccessibilityInfo, Alert, View } from 'react-native';

import { Aviso, type TipoAviso } from '@/componentes/Aviso';
import { Botao } from '@/componentes/Botao';
import { Carregando } from '@/componentes/Carregando';
import { Cartao } from '@/componentes/Cartao';
import { useAparenciaCapacete } from '@/componentes/EstadoCapacete';
import { FaixaRisco, TEXTO_FAIXA } from '@/componentes/FaixaRisco';
import { IconeEmCirculo } from '@/componentes/IconeEmCirculo';
import { EstadoVazio } from '@/componentes/EstadoVazio';
import { dadosDoTurno, IndiceRisco } from '@/componentes/IndiceRisco';
import { LinhaInfo } from '@/componentes/LinhaInfo';
import { Tela } from '@/componentes/Tela';
import { Texto } from '@/componentes/Texto';
import { TrechosCriticos } from '@/componentes/TrechosCriticos';
import { ALERTA_CALOR_C } from '@/config/constantes';
import { useTema } from '@/contextos/TemaContext';
import { useTurno, type TurnoAtual } from '@/contextos/TurnoContext';
import { useMinhaPosicao } from '@/hooks/useMinhaPosicao';
import { useTrechosCriticos } from '@/hooks/useTrechosCriticos';
import { converterErro } from '@/servicos/erros';
import { formatarDecimal, formatarDiaHora, jaPassou } from '@/utils/datas';
import { MENSAGEM_SEM_CONEXAO, mensagemComum } from '@/utils/mensagensErro';

/** Mensagem quando o comando não chegou ao capacete (503 nas rotas do turno; nada foi gravado). */
const MENSAGEM_CAPACETE_NAO_RESPONDEU = 'O capacete não respondeu. Confira se ele está ligado e tente de novo.';

/** Aviso mostrado na tela, com o tipo e o texto. */
interface AvisoTela {
  tipo: TipoAviso;
  mensagem: string;
}

/**
 * Turno em andamento (tela da pilha, aberta pela Início ou logo depois de começar o turno). Com o
 * turno aberto: horários, estado do capacete, Índice de Risco atualizado pelo
 * `GET /turnos/{id}/risco` (ou o último salvo, sem conexão), trechos críticos e "Encerrar turno".
 * Depois de encerrar, a tela mostra o aviso e o botão para voltar ao início.
 */
export default function TurnoAtivo() {
  const { turno, carregando } = useTurno();
  const [avisoFinal, setAvisoFinal] = useState<AvisoTela | null>(null);

  if (carregando) {
    return <Carregando mensagem="Abrindo o turno..." />;
  }
  if (turno) {
    return <TurnoAberto turno={turno} aoEncerrar={setAvisoFinal} />;
  }
  return (
    <Tela>
      {avisoFinal ? (
        <Aviso tipo={avisoFinal.tipo} mensagem={avisoFinal.mensagem} fechavel aoFechar={() => setAvisoFinal(null)} />
      ) : (
        <EstadoVazio titulo="Nenhum turno em andamento" icone="speedometer-outline" />
      )}
      {avisoFinal ? (
        <Botao titulo="Voltar ao início" icone="home" aoPressionar={() => router.dismissTo('/inicio')} />
      ) : (
        <Botao titulo="Começar turno" icone="play" aoPressionar={() => router.replace('/turno/novo')} />
      )}
    </Tela>
  );
}

/** Turno aberto: horários, estado do capacete, Índice de Risco, trechos e encerramento. */
function TurnoAberto({ turno, aoEncerrar }: { turno: TurnoAtual; aoEncerrar: (aviso: AvisoTela) => void }) {
  const { espaco } = useTema();
  const { atualizarRisco, encerrar } = useTurno();
  const [atualizando, setAtualizando] = useState(false);
  const [semConexao, setSemConexao] = useState(false);
  const [aviso, setAviso] = useState<AvisoTela | null>(null);
  const [encerrando, setEncerrando] = useState(false);
  const { alerta, alertaSemConexao, emergencia } = useTurno();
  const trechos = useTrechosCriticos();
  const posicao = useMinhaPosicao();

  // Calor: aviso quando o índice de calor é maior ou igual a ALERTA_CALOR_C (seção 5).
  const calorForte = alerta?.indiceCalorC != null && alerta.indiceCalorC >= ALERTA_CALOR_C;
  const calorAnterior = useRef(false);
  useEffect(() => {
    // Anuncia só quando o aviso PASSA a valer, não a cada consulta de 5 s.
    if (calorForte && !calorAnterior.current && alerta?.indiceCalorC != null) {
      AccessibilityInfo.announceForAccessibility(
        `Calor forte agora, índice de calor de ${formatarDecimal(alerta.indiceCalorC)} graus, medido pelo seu capacete. Pare na sombra e beba água.`,
      );
    }
    calorAnterior.current = calorForte;
  }, [calorForte, alerta?.indiceCalorC]);

  const atualizar = useCallback(async () => {
    setAtualizando(true);
    try {
      await atualizarRisco();
      setSemConexao(false);
      setAviso(null);
    } catch (e) {
      const erro = converterErro(e);
      if (erro.tipo === 'sem-conexao') {
        // Sem rede, a tela continua com o Índice salvo no SQLite.
        setSemConexao(true);
      } else if (erro.tipo === 'nao-encontrado') {
        setAviso({ tipo: 'atencao', mensagem: 'Este turno não foi encontrado na API. Você pode encerrá-lo.' });
      } else {
        setAviso({ tipo: 'erro', mensagem: mensagemComum(erro) });
      }
    } finally {
      setAtualizando(false);
    }
  }, [atualizarRisco]);

  // Atualiza o Índice ao abrir a aba e ao voltar para ela. O `atualizar` não muda quando
  // chega um Índice novo (o contexto lê o turno por ref), então não entra em repetição.
  useFocusEffect(
    useCallback(() => {
      void atualizar();
    }, [atualizar]),
  );

  async function confirmarEncerrar() {
    setEncerrando(true);
    setAviso(null);
    try {
      const resultado = await encerrar();
      const mensagem =
        resultado === 'encerrado'
          ? 'Turno encerrado. Bom descanso!'
          : 'Este turno não existia mais na API. Ele foi encerrado só neste aparelho.';
      AccessibilityInfo.announceForAccessibility(mensagem);
      aoEncerrar({ tipo: resultado === 'encerrado' ? 'sucesso' : 'atencao', mensagem });
    } catch (e) {
      const erro = converterErro(e);
      if (erro.tipo === 'conflito') {
        // 409 resolvível: se o app sabe qual é a emergência, oferece abrir a tela dela.
        setAviso({
          tipo: 'erro',
          mensagem:
            emergencia?.tipo === 'alerta'
              ? 'Há um alerta de queda em andamento. Resolva o alerta antes de encerrar.'
              : emergencia?.tipo === 'ocorrencia'
                ? 'Há uma ocorrência (SOS) em andamento. Resolva a ocorrência antes de encerrar.'
                : 'Há um alerta ou uma ocorrência em andamento. Resolva antes de encerrar.',
        });
      } else if (erro.tipo === 'indisponivel') {
        setAviso({ tipo: 'erro', mensagem: MENSAGEM_CAPACETE_NAO_RESPONDEU });
      } else if (erro.tipo === 'sem-conexao') {
        setAviso({ tipo: 'erro', mensagem: `O turno continua aberto. ${MENSAGEM_SEM_CONEXAO}` });
      } else {
        setAviso({ tipo: 'erro', mensagem: mensagemComum(erro) });
      }
      setEncerrando(false);
    }
  }

  function pedirConfirmacao() {
    Alert.alert('Encerrar o turno?', 'A proteção contra quedas será desligada.', [
      { text: 'Continuar rodando', style: 'cancel' },
      { text: 'Encerrar', style: 'destructive', onPress: () => void confirmarEncerrar() },
    ]);
  }

  const dados = dadosDoTurno(turno);
  const capacete = useAparenciaCapacete(turno.protecaoAtiva !== false ? (alerta?.estadoCapacete ?? null) : null);
  const capaceteSemConexao = turno.protecaoAtiva !== false && alertaSemConexao;
  const horarioTerminou = turno.fimPrevisto !== null && jaPassou(turno.fimPrevisto);

  /** Abre a tela da emergência em andamento (alerta ou ocorrência da vítima). */
  function abrirEmergencia() {
    if (emergencia?.tipo === 'alerta') {
      router.push('/alerta');
    } else if (emergencia?.tipo === 'ocorrencia') {
      router.push({ pathname: '/ocorrencia/[id]', params: { id: emergencia.ocorrenciaId, papel: 'VITIMA' } });
    }
  }

  return (
    <Tela
      aoAtualizar={() => {
        void atualizar();
        void trechos.atualizar();
        void posicao.atualizar();
      }}
      atualizando={atualizando}
    >
      {/* Resumo no topo: o estado do capacete, a faixa e o Índice, lidos juntos. */}
      <Cartao>
        <View
          accessible
          accessibilityLabel={`Seu capacete: ${capacete.texto}.${
            turno.indiceDisponivel ? ` Índice ${turno.indiceRisco} de 100, ${TEXTO_FAIXA[turno.faixaRisco].toLowerCase()}.` : ''
          }${capaceteSemConexao ? ' Sem conexão: pode estar desatualizado.' : ''}`}
          accessibilityLiveRegion="polite"
          style={{ gap: espaco.medio }}
        >
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: espaco.medio }}>
            <IconeEmCirculo nome={capacete.icone} tom={capacete.tomCirculo} tamanho={56} />
            <View style={{ flex: 1 }}>
              <Texto variante="legenda">Seu capacete</Texto>
              <Texto variante="subtitulo">{capacete.texto}</Texto>
            </View>
          </View>
          {turno.indiceDisponivel ? (
            <View style={{ flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', gap: espaco.pequeno }}>
              <FaixaRisco faixa={turno.faixaRisco} />
              <Texto variante="rotulo">{`Índice ${turno.indiceRisco} de 100`}</Texto>
            </View>
          ) : null}
          {capaceteSemConexao ? (
            <Texto variante="legenda">Sem conexão: o estado do capacete pode estar desatualizado.</Texto>
          ) : null}
        </View>
      </Cartao>

      {calorForte && alerta?.indiceCalorC != null ? (
        <Aviso
          tipo="atencao"
          mensagem={`Calor forte agora (índice de calor de ${formatarDecimal(alerta.indiceCalorC)} °C). Pare na sombra e beba água.\nMedido pelo seu capacete agora.`}
        />
      ) : null}

      {/* Só com SOS em andamento (o alerta de queda abre sozinho). */}
      {emergencia?.tipo === 'ocorrencia' ? (
        <Botao titulo="Abrir a ocorrência" variante="perigo" icone="alert-circle" aoPressionar={abrirEmergencia} />
      ) : null}

      {semConexao ? (
        <Aviso
          tipo="atencao"
          mensagem={`Sem conexão: mostrando o último Índice salvo (${formatarDiaHora(turno.atualizadoEm)}).`}
        />
      ) : null}
      {horarioTerminou ? (
        <Aviso tipo="atencao" mensagem="O horário previsto terminou. Encerre o turno quando parar de rodar." />
      ) : null}

      {/* Turno restaurado pela sessão (aberto em outro aparelho ou antes do login): a API não
          devolve os horários, então o cartão mostra só que o turno está em andamento. */}
      <Cartao titulo="Horários">
        {turno.inicio !== null && turno.fimPrevisto !== null ? (
          <>
            <LinhaInfo rotulo="Começou" valor={formatarDiaHora(turno.inicio)} />
            <LinhaInfo rotulo="Fim previsto" valor={formatarDiaHora(turno.fimPrevisto)} />
          </>
        ) : (
          <Texto>Turno em andamento, aberto antes neste ou em outro celular. Os horários aparecem no histórico depois de encerrar.</Texto>
        )}
        {turno.protecaoAtiva !== null ? (
          <LinhaInfo rotulo="Proteção contra quedas" valor={turno.protecaoAtiva ? 'Ligada' : 'Desligada'} />
        ) : null}
      </Cartao>

      <Cartao titulo="Risco do turno">
        {turno.indiceDisponivel ? (
          <IndiceRisco dados={dados} anunciarMudancas />
        ) : (
          <Texto>O Índice de Risco deste turno não carregou agora.</Texto>
        )}
        <Texto variante="legenda">Puxe a tela para baixo para atualizar o Índice.</Texto>
      </Cartao>

      <TrechosCriticos estado={trechos} posicao={posicao} />

      {aviso ? <Aviso tipo={aviso.tipo} mensagem={aviso.mensagem} /> : null}
      {aviso && emergencia ? (
        <Botao
          titulo={emergencia.tipo === 'alerta' ? 'Abrir o alerta' : 'Abrir a ocorrência'}
          variante="secundaria"
          compacto
          aoPressionar={abrirEmergencia}
        />
      ) : null}
      <Botao
        titulo="Encerrar turno"
        variante="perigo"
        icone="stop-circle"
        aoPressionar={pedirConfirmacao}
        carregando={encerrando}
        accessibilityHint="Pede confirmação antes de encerrar"
      />
    </Tela>
  );
}
