import { router } from 'expo-router';
import { AccessibilityInfo, Alert, View } from 'react-native';

import { Aviso } from '@/componentes/Aviso';
import { Botao } from '@/componentes/Botao';
import { Cartao } from '@/componentes/Cartao';
import { Icone } from '@/componentes/Icone';
import { tipoDaConta } from '@/componentes/MenuConta';
import { Tela } from '@/componentes/Tela';
import { Texto } from '@/componentes/Texto';
import { MAXIMO_CONTAS_APARELHO } from '@/config/constantes';
import { useChamados } from '@/contextos/ChamadosContext';
import { useSessao } from '@/contextos/SessaoContext';
import { useTema } from '@/contextos/TemaContext';
import { useTurno } from '@/contextos/TurnoContext';
import { useSessaoDaConta } from '@/hooks/useSessaoDaConta';
import { comecarPilhaEm } from '@/utils/navegacao';

/** Id único de cada troca de conta (o instante da troca), chamado só no toque de "Trocar". */
function novoIdDeTroca(): string {
  return String(Date.now());
}

/** Selo "Em uso", com ícone e texto (a cor nunca é a única pista). */
function SeloEmUso() {
  const { cores, espaco, raio } = useTema();
  const cor = cores.aviso.sucesso;
  return (
    <View
      style={{
        flexDirection: 'row',
        alignItems: 'center',
        alignSelf: 'flex-start',
        gap: espaco.pequeno,
        backgroundColor: cor.fundo,
        borderRadius: raio.pilula,
        paddingHorizontal: espaco.medio,
        paddingVertical: espaco.pequeno,
      }}
    >
      <Icone nome="checkmark-circle" cor={cor.icone} />
      <Texto variante="rotulo" corPersonalizada={cor.texto}>
        Em uso
      </Texto>
    </View>
  );
}

/** Props do cartão de uma conta. */
interface CartaoContaProps {
  entregadorId: string;
  emUso: boolean;
  semSessao: boolean;
  aoTrocar: (entregadorId: string, exibido: string) => void;
  aoSair: (entregadorId: string, exibido: string) => void;
}

/**
 * Cartão de uma conta salva: o título é o nome (do `GET /entregadores/sessao` com o token dessa
 * conta, só em memória); enquanto carrega ou sem conexão, "Conta salva". O subtítulo diz se tem
 * capacete e se é voluntário. Conta com a sessão terminada pede para entrar de novo.
 */
function CartaoConta({ entregadorId, emUso, semSessao, aoTrocar, aoSair }: CartaoContaProps) {
  const { sessao } = useSessaoDaConta(entregadorId, !semSessao);
  const exibido = sessao?.nome ?? 'Conta salva';
  return (
    <Cartao titulo={exibido}>
      {sessao ? <Texto cor="textoSecundario">{tipoDaConta(sessao.codigoDispositivo, sessao.voluntarioId)}</Texto> : null}
      {emUso ? <SeloEmUso /> : null}
      {semSessao ? (
        <>
          <Aviso tipo="atencao" mensagem="A sessão desta conta terminou. Entre de novo com o e-mail e a senha." />
          <Botao
            titulo="Entrar de novo"
            icone="log-in"
            aoPressionar={() => router.push({ pathname: '/login', params: { modo: 'adicionar' } })}
            accessibilityLabel={`Entrar de novo na conta ${exibido}`}
          />
        </>
      ) : emUso ? null : (
        <Botao
          titulo="Trocar para esta conta"
          icone="swap-horizontal"
          aoPressionar={() => aoTrocar(entregadorId, exibido)}
          accessibilityLabel={`Trocar para a conta ${exibido}`}
          accessibilityHint="Pede confirmação antes de trocar. Não precisa digitar a senha."
        />
      )}
      <Botao
        titulo="Sair desta conta"
        variante="secundaria"
        icone="log-out"
        aoPressionar={() => aoSair(entregadorId, exibido)}
        accessibilityLabel={`Sair da conta ${exibido}`}
        accessibilityHint="Pede confirmação. O histórico salvo neste celular continua."
      />
    </Cartao>
  );
}

/**
 * Contas neste aparelho (até `MAXIMO_CONTAS_APARELHO`, no expo-secure-store): um cartão por conta,
 * troca rápida sem senha, com confirmação (avisa de turno aberto, de emergência e de atendimento
 * aceito como voluntário), "Adicionar outra conta" e "Sair desta conta". Aberta pelo menu da
 * conta e pela aba Ajustes.
 */
export default function Contas() {
  const { contas, contasSemSessao, contaAtiva, sessao, trocarConta, sair, podeAdicionarConta } = useSessao();
  const { turno, emergencia } = useTurno();
  const { voluntarioId, rede, ocorrenciaAceita } = useChamados();
  const atual = sessao?.nome ?? 'atual';

  /**
   * Avisos sobre a conta atual que a pessoa precisa saber antes de trocar: emergência ou turno
   * aberto (como vítima) e atendimento aceito ou disponibilidade ligada (como voluntário).
   */
  function avisoAntesDeTrocar(): string {
    const avisos: string[] = [];
    if (emergencia?.tipo === 'alerta') {
      avisos.push(
        `Atenção: a conta ${atual} tem um alerta de queda em andamento. Depois da troca, este celular deixa de mostrar o alerta.`,
      );
    } else if (emergencia?.tipo === 'ocorrencia') {
      avisos.push(
        `Atenção: a conta ${atual} tem um SOS em andamento. Depois da troca, este celular deixa de acompanhar o socorro.`,
      );
    } else if (turno) {
      avisos.push(
        `Atenção: a conta ${atual} tem um turno aberto. Ele continua aberto e volta a aparecer quando você usar essa conta de novo.`,
      );
    }
    if (ocorrenciaAceita) {
      avisos.push(
        `Atenção: a conta ${atual} aceitou um chamado de socorro que ainda está em andamento. O entregador continua esperando. Volte a esta conta para acompanhar e encerrar o atendimento.`,
      );
    } else if (voluntarioId && rede.disponivel) {
      // A API continua com o último PUT: o voluntário segue disponível mesmo com outra conta em uso.
      avisos.push(
        `A conta ${atual} continua disponível na rede de socorro, mas este celular só avisa de chamados novos quando você voltar a ela.`,
      );
    }
    return avisos.map((aviso) => `\n\n${aviso}`).join('');
  }

  /** `exibido` é o nome da conta quando ele já chegou da API, ou "Conta salva". */
  function confirmarTroca(entregadorId: string, exibido: string) {
    Alert.alert(
      'Trocar de conta?',
      `Trocar para a conta ${exibido}? O turno, os chamados e o histórico mostrados passam a ser desta conta.${avisoAntesDeTrocar()}`,
      [
        { text: 'Cancelar', style: 'cancel' },
        { text: 'Trocar', onPress: () => void trocar(entregadorId) },
      ],
    );
  }

  async function trocar(entregadorId: string) {
    await trocarConta(entregadorId);
    AccessibilityInfo.announceForAccessibility('Conta trocada');
    // Volta para a aba Turno, que mostra o aviso da troca uma vez só (pelo id único da troca).
    // Nenhum dado pessoal vai na rota: o nome vem da sessão, na própria aba.
    router.dismissTo({ pathname: '/turno', params: { troca: novoIdDeTroca() } });
  }

  function confirmarSaida(entregadorId: string, exibido: string) {
    const emUso = entregadorId === contaAtiva;
    Alert.alert(
      'Sair desta conta?',
      `A conta ${exibido} sai deste celular. O histórico salvo aqui continua e volta quando você entrar de novo.${
        emUso ? avisoAntesDeTrocar() : ''
      }`,
      [
        { text: 'Cancelar', style: 'cancel' },
        { text: 'Sair', style: 'destructive', onPress: () => void sairDaConta(entregadorId, emUso) },
      ],
    );
  }

  async function sairDaConta(entregadorId: string, emUso: boolean) {
    const proxima = await sair(entregadorId);
    AccessibilityInfo.announceForAccessibility('Você saiu da conta');
    if (!emUso) {
      return;
    }
    if (proxima) {
      router.dismissTo({ pathname: '/turno', params: { troca: novoIdDeTroca() } });
    } else {
      comecarPilhaEm('/login');
    }
  }

  const cabeMais = podeAdicionarConta();

  return (
    <Tela>
      <Texto>{`Cada conta fica salva neste celular para trocar sem digitar a senha (até ${MAXIMO_CONTAS_APARELHO}).`}</Texto>
      {contas.map((id) => (
        <CartaoConta
          key={id}
          entregadorId={id}
          emUso={id === contaAtiva}
          semSessao={contasSemSessao.includes(id)}
          aoTrocar={confirmarTroca}
          aoSair={confirmarSaida}
        />
      ))}
      {cabeMais ? null : (
        <Aviso
          tipo="informacao"
          mensagem={`Este celular já tem ${MAXIMO_CONTAS_APARELHO} contas, o máximo. Para adicionar outra, saia de uma delas.`}
        />
      )}
      <Botao
        titulo="Adicionar outra conta"
        icone="person-add"
        aoPressionar={() => router.push({ pathname: '/login', params: { modo: 'adicionar' } })}
        desabilitado={!cabeMais}
        accessibilityHint={cabeMais ? 'Abre o login para entrar com outra conta' : 'Limite de contas atingido'}
      />
    </Tela>
  );
}
