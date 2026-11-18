import { Redirect, router } from 'expo-router';
import { useState } from 'react';
import { AccessibilityInfo, Alert, View } from 'react-native';

import { Aviso, type TipoAviso } from '@/componentes/Aviso';
import { Botao } from '@/componentes/Botao';
import { Carregando } from '@/componentes/Carregando';
import { Chave } from '@/componentes/Chave';
import { GrupoOpcoes } from '@/componentes/GrupoOpcoes';
import { dadosDoTurno, fraseIndiceRisco } from '@/componentes/IndiceRisco';
import { Tela } from '@/componentes/Tela';
import { Texto } from '@/componentes/Texto';
import { useSessao } from '@/contextos/SessaoContext';
import { useTema } from '@/contextos/TemaContext';
import { useTurno } from '@/contextos/TurnoContext';
import { converterErro, type ErroApi } from '@/servicos/erros';
import { lerPermissao, notificacoes, pedirPermissao } from '@/servicos/notificacoes';
import { mensagemComum } from '@/utils/mensagensErro';

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

/**
 * Antes do primeiro turno, explica por que o app quer notificar e só então pede a permissão.
 * Negar não muda nada no app: ele só deixa de notificar. Resolve quando a pessoa responde.
 */
function explicarEPedirNotificacoes(): Promise<void> {
  return new Promise((resolver) => {
    Alert.alert(
      'Avisos do HelmCare',
      'Para avisar você de uma queda percebida pelo capacete, do socorro a caminho e de calor forte, mesmo com o app minimizado, o HelmCare precisa mandar notificações.',
      [
        { text: 'Agora não', style: 'cancel', onPress: () => resolver() },
        { text: 'Permitir', onPress: () => void pedirPermissao().finally(() => resolver()) },
      ],
      { cancelable: true, onDismiss: () => resolver() },
    );
  });
}

/**
 * Começar o turno (tela da pilha, aberta pela Início): duração, proteção contra quedas e
 * `POST /turnos`. Ao começar, esta tela é trocada pelo Turno em andamento (`router.replace`).
 * Com um turno já aberto, vai direto para ele.
 */
export default function NovoTurno() {
  const { espaco } = useTema();
  const { contaAtiva, sessao } = useSessao();
  const { turno, carregando, iniciar } = useTurno();
  // O capacete vem da sessão (contrato 1.15). Sem sessão (sem conexão), a proteção fica desligada.
  const temCapacete = sessao?.codigoDispositivo != null;
  const [duracao, setDuracao] = useState<Duracao>('4');
  // A proteção começa ligada quando há capacete; sem capacete, não pode ligar.
  const [protecaoEscolhida, setProtecaoEscolhida] = useState<boolean | null>(null);
  const protecao = temCapacete && (protecaoEscolhida ?? true);
  const [aviso, setAviso] = useState<AvisoTela | null>(null);
  const [enviando, setEnviando] = useState(false);

  if (!contaAtiva || carregando) {
    return <Carregando mensagem="Abrindo o turno..." />;
  }
  if (turno && !enviando) {
    return <Redirect href="/turno/ativo" />;
  }

  function alternarProtecao() {
    if (temCapacete) {
      setProtecaoEscolhida(!protecao);
    }
  }

  async function comecar() {
    setEnviando(true);
    setAviso(null);
    try {
      // Primeiro turno neste aparelho: a frase explica o motivo antes do pedido do sistema.
      if ((await lerPermissao()) === 'nao-pedida') {
        await explicarEPedirNotificacoes();
      }
      const novo = await iniciar(Number(duracao), protecao);
      if (novo) {
        AccessibilityInfo.announceForAccessibility(`Turno começou. ${fraseIndiceRisco(dadosDoTurno(novo))}`);
        void notificacoes.turnoIniciado(novo.faixaRisco);
        router.replace('/turno/ativo');
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
    <Tela>
      <Texto variante="titulo">Começar o turno</Texto>

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
          ligada={protecao}
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
        aoPressionar={() => void comecar()}
        carregando={enviando}
        accessibilityHint="Começa o turno e mostra o Índice de Risco"
      />
    </Tela>
  );
}
