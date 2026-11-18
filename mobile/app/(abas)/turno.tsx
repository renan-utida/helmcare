import { router, useFocusEffect, useLocalSearchParams } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';
import { AccessibilityInfo, Alert, View } from 'react-native';

import { Aviso, type TipoAviso } from '@/componentes/Aviso';
import { Botao } from '@/componentes/Botao';
import { Carregando } from '@/componentes/Carregando';
import { Cartao } from '@/componentes/Cartao';
import { Chave } from '@/componentes/Chave';
import { EstadoCapacete } from '@/componentes/EstadoCapacete';
import { EstadoVazio } from '@/componentes/EstadoVazio';
import { GrupoOpcoes } from '@/componentes/GrupoOpcoes';
import { fraseIndiceRisco, IndiceRisco, type DadosIndiceRisco } from '@/componentes/IndiceRisco';
import { LinhaInfo } from '@/componentes/LinhaInfo';
import { Tela } from '@/componentes/Tela';
import { Texto } from '@/componentes/Texto';
import { TrechosCriticos } from '@/componentes/TrechosCriticos';
import { ALERTA_CALOR_C } from '@/config/constantes';
import { useSessao } from '@/contextos/SessaoContext';
import { useTema } from '@/contextos/TemaContext';
import { useTurno, type TurnoAtual } from '@/contextos/TurnoContext';
import { useTrechosCriticos } from '@/hooks/useTrechosCriticos';
import { converterErro, type ErroApi } from '@/servicos/erros';
import { formatarDecimal, formatarDiaHora, jaPassou } from '@/utils/datas';
import { MENSAGEM_SEM_CONEXAO, mensagemComum } from '@/utils/mensagensErro';

/** Durações oferecidas em botões (em horas). O contrato aceita até 24 h. */
const DURACOES = ['4', '8', '12'] as const;
type Duracao = (typeof DURACOES)[number];

/** Mensagem quando o comando não chegou ao capacete (503 nas rotas do turno; nada foi gravado). */
const MENSAGEM_CAPACETE_NAO_RESPONDEU = 'O capacete não respondeu. Confira se ele está ligado e tente de novo.';

/**
 * Id da última troca de conta já avisada nesta aba. Fica fora do componente para valer
 * mesmo se a aba for recriada; só guarda um número de controle, nenhum dado pessoal.
 */
let ultimaTrocaMostrada: string | null = null;

/** Aviso mostrado na tela, com o tipo e o texto. */
interface AvisoTela {
  tipo: TipoAviso;
  mensagem: string;
}

/** Converte o turno no formato que o componente do Índice usa. */
function dadosDoIndice(turno: TurnoAtual): DadosIndiceRisco {
  return {
    indiceRisco: turno.indiceRisco,
    faixaRisco: turno.faixaRisco,
    componentes: {
      sinistro: turno.componenteSinistro,
      chuva: turno.componenteChuva,
      calor: turno.componenteCalor,
    },
    explicacao: turno.explicacao,
  };
}

/**
 * Aba Turno. Sem turno aberto: escolha da duração, proteção contra quedas e início
 * (`POST /turnos`). Com turno aberto: horários, Índice de Risco atualizado pelo
 * `GET /turnos/{id}/risco` (ou o último salvo, sem conexão) e o botão de encerrar.
 */
export default function Turno() {
  const { contaAtiva, sessao } = useSessao();
  const { turno, carregando } = useTurno();
  // Aviso que sobrevive à troca de estado (ex.: "Turno encerrado" ao voltar para o início).
  const [avisoFinal, setAvisoFinal] = useState<AvisoTela | null>(null);
  // Id único da troca de conta feita em Contas (pela rota). Nenhum dado pessoal vai na rota.
  const { troca } = useLocalSearchParams<{ troca?: string }>();
  const [trocaVisivel, setTrocaVisivel] = useState(false);
  // O nome vem da sessão da conta nova, que pode chegar um pouco depois do aviso.
  const avisoTroca = trocaVisivel
    ? sessao
      ? `Você está usando a conta de ${sessao.nome}`
      : 'Você trocou de conta.'
    : null;

  // O aviso aparece uma vez por troca: ao ganhar o foco, mostra se aquela troca ainda não foi
  // mostrada e marca como mostrada; ao sair da aba, some. Ao voltar, o parâmetro antigo continua
  // na rota, mas a troca já está marcada e o aviso não reaparece.
  useFocusEffect(
    useCallback(() => {
      if (troca && troca !== ultimaTrocaMostrada) {
        ultimaTrocaMostrada = troca;
        setTrocaVisivel(true);
      }
      return () => setTrocaVisivel(false);
    }, [troca]),
  );

  if (!contaAtiva) {
    return (
      <Tela dentroDeAbas>
        <EstadoVazio titulo="Nenhuma conta neste aparelho" icone="person-outline" />
        <Botao titulo="Entrar" aoPressionar={() => router.replace('/login')} />
      </Tela>
    );
  }
  if (carregando) {
    return <Carregando mensagem="Abrindo o turno..." />;
  }
  if (turno) {
    return <TurnoAtivo turno={turno} aoEncerrar={setAvisoFinal} avisoTroca={avisoTroca} />;
  }
  // O capacete vem da sessão (contrato 1.15). Sem sessão (sem conexão), a proteção fica desligada.
  const temCapacete = sessao?.codigoDispositivo != null;
  return (
    <NovoTurno
      // A `key` recria o formulário quando a conta ou o capacete mudam, e a chave de
      // proteção volta ao padrão (ligada com capacete, desligada sem).
      key={`${contaAtiva}-${temCapacete}`}
      temCapacete={temCapacete}
      avisoFinal={avisoFinal}
      limparAvisoFinal={() => setAvisoFinal(null)}
      avisoTroca={avisoTroca}
    />
  );
}

/** Estado sem turno aberto: duração, proteção e o botão de começar. */
function NovoTurno({
  temCapacete,
  avisoFinal,
  limparAvisoFinal,
  avisoTroca,
}: {
  temCapacete: boolean;
  avisoFinal: AvisoTela | null;
  avisoTroca: string | null;
  limparAvisoFinal: () => void;
}) {
  const { espaco } = useTema();
  const { iniciar } = useTurno();
  const [duracao, setDuracao] = useState<Duracao>('4');
  // A proteção começa ligada quando há capacete; sem capacete, não pode ligar.
  const [protecao, setProtecao] = useState(temCapacete);
  const [aviso, setAviso] = useState<AvisoTela | null>(null);
  const [enviando, setEnviando] = useState(false);

  function alternarProtecao() {
    if (temCapacete) {
      setProtecao((atual) => !atual);
    }
  }

  async function comecar() {
    setEnviando(true);
    setAviso(null);
    limparAvisoFinal();
    try {
      const novo = await iniciar(Number(duracao), protecao && temCapacete);
      // O contexto troca a tela para o turno ativo; aqui só anuncia o Índice em frase.
      if (novo) {
        AccessibilityInfo.announceForAccessibility(`Turno começou. ${fraseIndiceRisco(dadosDoIndice(novo))}`);
      }
    } catch (e) {
      const erro: ErroApi = converterErro(e);
      if (erro.tipo === 'conflito') {
        setAviso({
          tipo: 'erro',
          mensagem:
            'Não foi possível começar: já existe um turno ativo neste cadastro ou o capacete não está vinculado.',
        });
      } else if (erro.tipo === 'indisponivel') {
        setAviso({ tipo: 'erro', mensagem: MENSAGEM_CAPACETE_NAO_RESPONDEU });
      } else if (erro.tipo === 'validacao') {
        setAviso({ tipo: 'erro', mensagem: 'A duração do turno não foi aceita. Escolha 4, 8 ou 12 horas.' });
      } else if (erro.tipo === 'nao-encontrado') {
        setAviso({ tipo: 'erro', mensagem: 'Cadastro não encontrado na API. Confira a conta em uso no menu da conta.' });
      } else {
        setAviso({ tipo: 'erro', mensagem: mensagemComum(erro) });
      }
    } finally {
      setEnviando(false);
    }
  }

  return (
    <Tela dentroDeAbas>
      <Texto variante="titulo">Começar o turno</Texto>
      {avisoTroca ? <Aviso tipo="informacao" mensagem={avisoTroca} /> : null}
      {avisoFinal ? <Aviso tipo={avisoFinal.tipo} mensagem={avisoFinal.mensagem} /> : null}

      <GrupoOpcoes
        rotulo="Quanto tempo vai rodar hoje?"
        opcoes={DURACOES}
        selecionada={duracao}
        aoSelecionar={setDuracao}
        textoDe={(horas) => `${horas} h`}
        leituraDe={(horas) => `${horas} horas`}
        colunas={3}
      />

      <View style={{ gap: espaco.pequeno }}>
        <Chave
          rotulo="Proteção contra quedas"
          ligada={protecao && temCapacete}
          aoAlternar={alternarProtecao}
          desabilitada={!temCapacete}
          accessibilityHint={
            temCapacete
              ? 'Liga o capacete para perceber quedas durante o turno'
              : 'Vincule um capacete no Perfil para ligar a proteção'
          }
        />
        {temCapacete ? null : (
          <>
            <Texto cor="textoSecundario">Vincule um capacete no Perfil para ligar a proteção.</Texto>
            <Botao titulo="Abrir o Perfil" variante="secundaria" aoPressionar={() => router.navigate('/perfil')} />
          </>
        )}
      </View>

      {aviso ? <Aviso tipo={aviso.tipo} mensagem={aviso.mensagem} /> : null}
      <Botao
        titulo="Ver meu risco e começar"
        icone="play"
        aoPressionar={comecar}
        carregando={enviando}
        accessibilityHint="Começa o turno e mostra o Índice de Risco"
      />
    </Tela>
  );
}

/** Estado com turno aberto: horários, Índice de Risco e encerramento. */
function TurnoAtivo({
  turno,
  aoEncerrar,
  avisoTroca,
}: {
  turno: TurnoAtual;
  aoEncerrar: (aviso: AvisoTela) => void;
  avisoTroca: string | null;
}) {
  const { atualizarRisco, encerrar } = useTurno();
  const [atualizando, setAtualizando] = useState(false);
  const [semConexao, setSemConexao] = useState(false);
  const [aviso, setAviso] = useState<AvisoTela | null>(null);
  const [encerrando, setEncerrando] = useState(false);
  const { alerta, alertaSemConexao, emergencia } = useTurno();
  const trechos = useTrechosCriticos();

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

  const dados = dadosDoIndice(turno);
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
      dentroDeAbas
      aoAtualizar={() => {
        void atualizar();
        void trechos.atualizar();
      }}
      atualizando={atualizando}
    >
      <Texto variante="titulo">Turno em andamento</Texto>
      {avisoTroca ? <Aviso tipo="informacao" mensagem={avisoTroca} /> : null}

      {calorForte && alerta?.indiceCalorC != null ? (
        <Aviso
          tipo="atencao"
          mensagem={`Calor forte agora (índice de calor de ${formatarDecimal(alerta.indiceCalorC)} °C). Pare na sombra e beba água.\nMedido pelo seu capacete agora.`}
        />
      ) : null}

      {emergencia ? (
        <Botao
          titulo={emergencia.tipo === 'alerta' ? 'Abrir o alerta de queda' : 'Abrir a ocorrência'}
          variante="perigo"
          icone="alert-circle"
          aoPressionar={abrirEmergencia}
        />
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
      <Cartao>
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

      <EstadoCapacete
        estado={turno.protecaoAtiva !== false ? (alerta?.estadoCapacete ?? null) : null}
        semConexao={turno.protecaoAtiva !== false && alertaSemConexao}
      />

      <Cartao>
        {turno.indiceDisponivel ? (
          <IndiceRisco dados={dados} anunciarMudancas />
        ) : (
          <Texto>O Índice de Risco deste turno não carregou agora.</Texto>
        )}
        <Texto variante="legenda">Puxe a tela para baixo para atualizar o Índice.</Texto>
      </Cartao>

      <TrechosCriticos estado={trechos} />

      {aviso ? <Aviso tipo={aviso.tipo} mensagem={aviso.mensagem} /> : null}
      {aviso && emergencia ? (
        <Botao
          titulo={emergencia.tipo === 'alerta' ? 'Abrir o alerta' : 'Abrir a ocorrência'}
          variante="secundaria"
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
