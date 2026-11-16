import { router, useFocusEffect, useLocalSearchParams } from 'expo-router';
import { useCallback, useState } from 'react';
import { AccessibilityInfo, Alert, Pressable, Switch, View } from 'react-native';

import { Aviso, type TipoAviso } from '@/componentes/Aviso';
import { Botao } from '@/componentes/Botao';
import { Carregando } from '@/componentes/Carregando';
import { Cartao } from '@/componentes/Cartao';
import { EstadoVazio } from '@/componentes/EstadoVazio';
import { GrupoOpcoes } from '@/componentes/GrupoOpcoes';
import { fraseIndiceRisco, IndiceRisco, type DadosIndiceRisco } from '@/componentes/IndiceRisco';
import { LinhaInfo } from '@/componentes/LinhaInfo';
import { Tela } from '@/componentes/Tela';
import { Texto } from '@/componentes/Texto';
import { useSessao } from '@/contextos/SessaoContext';
import { useTema } from '@/contextos/TemaContext';
import { useTurno } from '@/contextos/TurnoContext';
import type { TurnoLocal } from '@/banco/turnoRepositorio';
import { converterErro, type ErroApi } from '@/servicos/erros';
import { formatarDiaHora, jaPassou } from '@/utils/datas';
import { MENSAGEM_SEM_CONEXAO, mensagemComum } from '@/utils/mensagensErro';

/** Durações oferecidas em botões (em horas). O contrato aceita até 24 h. */
const DURACOES = ['4', '8', '12'] as const;
type Duracao = (typeof DURACOES)[number];

/** Mensagem quando o comando não chegou ao capacete (503 nas rotas do turno; nada foi gravado). */
const MENSAGEM_CAPACETE_NAO_RESPONDEU = 'O capacete não respondeu. Confira se ele está ligado e tente de novo.';

/** Aviso mostrado na tela, com o tipo e o texto. */
interface AvisoTela {
  tipo: TipoAviso;
  mensagem: string;
}

/** Converte o turno salvo no formato que o componente do Índice usa. */
function dadosDoIndice(turno: TurnoLocal): DadosIndiceRisco {
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
  const { perfilAtivo } = useSessao();
  const { turno, carregando } = useTurno();
  // Aviso que sobrevive à troca de estado (ex.: "Turno encerrado" ao voltar para o início).
  const [avisoFinal, setAvisoFinal] = useState<AvisoTela | null>(null);
  // Apelido do perfil recém-escolhido na tela Perfis (vem pela rota, só depois da troca).
  const { perfilTrocado } = useLocalSearchParams<{ perfilTrocado?: string }>();
  const avisoTroca = perfilTrocado ? `Você está usando o perfil ${perfilTrocado}` : null;

  // O aviso da troca vale só até a pessoa sair da aba: ao perder o foco, o parâmetro é limpo.
  useFocusEffect(
    useCallback(() => {
      return () => {
        if (perfilTrocado) {
          router.setParams({ perfilTrocado: undefined });
        }
      };
    }, [perfilTrocado]),
  );

  if (!perfilAtivo) {
    return (
      <Tela dentroDeAbas>
        <EstadoVazio titulo="Nenhum perfil neste aparelho" icone="person-outline" />
        <Botao titulo="Fazer cadastro" aoPressionar={() => router.push('/cadastro')} />
      </Tela>
    );
  }
  if (carregando) {
    return <Carregando mensagem="Abrindo o turno..." />;
  }
  if (turno) {
    return <TurnoAtivo turno={turno} aoEncerrar={setAvisoFinal} avisoTroca={avisoTroca} />;
  }
  const temCapacete = perfilAtivo.codigoCapacete !== null;
  return (
    <NovoTurno
      // A `key` recria o formulário quando o perfil ou o capacete mudam, e a chave de
      // proteção volta ao padrão (ligada com capacete, desligada sem).
      key={`${perfilAtivo.entregadorId}-${temCapacete}`}
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
  const { cores, espaco, medidas } = useTema();
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
        setAviso({ tipo: 'erro', mensagem: 'Cadastro não encontrado na API. Confira o perfil nas Configurações.' });
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
        {/* A linha inteira é a chave: área de toque grande e uma leitura só. */}
        <Pressable
          onPress={alternarProtecao}
          disabled={!temCapacete}
          accessibilityRole="switch"
          accessibilityLabel="Proteção contra quedas"
          accessibilityState={{ checked: protecao && temCapacete, disabled: !temCapacete }}
          accessibilityHint={
            temCapacete
              ? 'Liga o capacete para perceber quedas durante o turno'
              : 'Vincule um capacete no Perfil para ligar a proteção'
          }
          style={{
            flexDirection: 'row',
            alignItems: 'center',
            gap: espaco.medio,
            minHeight: medidas.alturaBotao,
          }}
        >
          <View style={{ flex: 1 }}>
            <Texto variante="rotulo">Proteção contra quedas</Texto>
            <Texto variante="legenda">{protecao && temCapacete ? 'Ligada' : 'Desligada'}</Texto>
          </View>
          <Switch
            value={protecao && temCapacete}
            onValueChange={alternarProtecao}
            disabled={!temCapacete}
            trackColor={{ false: cores.borda, true: cores.primaria }}
            thumbColor={cores.superficie}
            // A linha já anuncia a chave; o Switch não entra no leitor de novo.
            accessible={false}
            importantForAccessibility="no-hide-descendants"
          />
        </Pressable>
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
  turno: TurnoLocal;
  aoEncerrar: (aviso: AvisoTela) => void;
  avisoTroca: string | null;
}) {
  const { atualizarRisco, encerrar } = useTurno();
  const [atualizando, setAtualizando] = useState(false);
  const [semConexao, setSemConexao] = useState(false);
  const [aviso, setAviso] = useState<AvisoTela | null>(null);
  const [encerrando, setEncerrando] = useState(false);

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
        setAviso({ tipo: 'erro', mensagem: 'Há um alerta ou uma ocorrência em andamento. Resolva antes de encerrar.' });
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
  const horarioTerminou = jaPassou(turno.fimPrevisto);

  return (
    <Tela dentroDeAbas aoAtualizar={() => void atualizar()} atualizando={atualizando}>
      <Texto variante="titulo">Turno em andamento</Texto>
      {avisoTroca ? <Aviso tipo="informacao" mensagem={avisoTroca} /> : null}

      {semConexao ? (
        <Aviso
          tipo="atencao"
          mensagem={`Sem conexão: mostrando o último Índice salvo (${formatarDiaHora(turno.atualizadoEm)}).`}
        />
      ) : null}
      {horarioTerminou ? (
        <Aviso tipo="atencao" mensagem="O horário previsto terminou. Encerre o turno quando parar de rodar." />
      ) : null}

      <Cartao>
        <LinhaInfo rotulo="Começou" valor={formatarDiaHora(turno.inicio)} />
        <LinhaInfo rotulo="Fim previsto" valor={formatarDiaHora(turno.fimPrevisto)} />
        <LinhaInfo rotulo="Proteção contra quedas" valor={turno.protecaoAtiva ? 'Ligada' : 'Desligada'} />
      </Cartao>

      <Cartao>
        <IndiceRisco dados={dados} anunciarMudancas />
        <Texto variante="legenda">Puxe a tela para baixo para atualizar o Índice.</Texto>
      </Cartao>

      {aviso ? <Aviso tipo={aviso.tipo} mensagem={aviso.mensagem} /> : null}
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
