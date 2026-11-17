import { router } from 'expo-router';
import { AccessibilityInfo, Alert, View } from 'react-native';

import type { Perfil } from '@/banco/perfilRepositorio';
import { Botao } from '@/componentes/Botao';
import { Cartao } from '@/componentes/Cartao';
import { Icone } from '@/componentes/Icone';
import { LinhaInfo } from '@/componentes/LinhaInfo';
import { Tela } from '@/componentes/Tela';
import { Texto } from '@/componentes/Texto';
import { useChamados } from '@/contextos/ChamadosContext';
import { APELIDO_COM_CAPACETE, APELIDO_SEM_CAPACETE, useSessao } from '@/contextos/SessaoContext';
import { useTema } from '@/contextos/TemaContext';
import { useTurno } from '@/contextos/TurnoContext';
import { useNomeEntregador } from '@/hooks/useNomeEntregador';

/** Id único de cada troca de perfil (o instante da troca), chamado só no toque de "Trocar". */
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

/** Props do cartão de um perfil. */
interface CartaoPerfilProps {
  perfil: Perfil;
  emUso: boolean;
  aoTrocar: (perfil: Perfil, exibido: string) => void;
  aoRemover: (perfil: Perfil, exibido: string) => void;
}

/**
 * Cartão de um perfil: o título é o nome do entregador (do `GET /entregadores/{id}`, só em
 * memória, nunca no SQLite); enquanto carrega ou sem conexão, é o apelido. O subtítulo diz o tipo.
 */
function CartaoPerfil({ perfil, emUso, aoTrocar, aoRemover }: CartaoPerfilProps) {
  const { nome } = useNomeEntregador(perfil.entregadorId);
  const exibido = nome ?? perfil.apelido;
  const tipo = perfil.codigoCapacete ? APELIDO_COM_CAPACETE : APELIDO_SEM_CAPACETE;
  return (
    <Cartao titulo={exibido}>
      <Texto cor="textoSecundario">{tipo}</Texto>
      {emUso ? <SeloEmUso /> : null}
      <LinhaInfo rotulo="Capacete" valor={perfil.codigoCapacete ?? 'Sem capacete'} />
      <LinhaInfo rotulo="Voluntário da rede" valor={perfil.voluntarioId ? 'Sim' : 'Não'} />
      {emUso ? null : (
        <>
          <Botao
            titulo="Trocar para este perfil"
            icone="swap-horizontal"
            aoPressionar={() => aoTrocar(perfil, exibido)}
            accessibilityLabel={`Trocar para o perfil ${exibido}`}
            accessibilityHint="Pede confirmação antes de trocar"
          />
          <Botao
            titulo="Remover deste aparelho"
            variante="secundaria"
            icone="trash"
            aoPressionar={() => aoRemover(perfil, exibido)}
            accessibilityLabel={`Remover o perfil ${exibido} deste aparelho`}
            accessibilityHint="Apaga só do celular. O cadastro continua na API."
          />
        </>
      )}
    </Cartao>
  );
}

/**
 * Perfis neste aparelho: um cartão por perfil, troca com confirmação (avisa de turno aberto,
 * de emergência e de atendimento aceito como voluntário)
 * e remoção só do celular. Aberta pelo cabeçalho das abas e pela aba Ajustes.
 */
export default function Perfis() {
  const { perfis, perfilAtivo, definirPerfilAtivo, removerPerfil } = useSessao();
  const { turno, emergencia } = useTurno();
  const { voluntarioId, rede, ocorrenciaAceita } = useChamados();

  /**
   * Avisos sobre o perfil atual que a pessoa precisa saber antes de trocar: emergência ou turno
   * aberto (como vítima) e atendimento aceito ou disponibilidade ligada (como voluntário).
   */
  function avisoAntesDeTrocar(): string {
    const atual = perfilAtivo?.apelido ?? 'atual';
    const avisos: string[] = [];
    if (emergencia?.tipo === 'alerta') {
      avisos.push(
        `Atenção: o perfil ${atual} tem um alerta de queda em andamento. Depois da troca, este celular deixa de mostrar o alerta.`,
      );
    } else if (emergencia?.tipo === 'ocorrencia') {
      avisos.push(
        `Atenção: o perfil ${atual} tem um SOS em andamento. Depois da troca, este celular deixa de acompanhar o socorro.`,
      );
    } else if (turno) {
      avisos.push(
        `Atenção: o perfil ${atual} tem um turno aberto. Ele continua aberto e volta a aparecer quando você usar esse perfil de novo.`,
      );
    }
    if (ocorrenciaAceita) {
      avisos.push(
        `Atenção: o perfil ${atual} aceitou um chamado de socorro que ainda está em andamento. O entregador continua esperando. Volte a este perfil para acompanhar e encerrar o atendimento.`,
      );
    } else if (voluntarioId && rede.disponivel) {
      // A API continua com o último PUT: o voluntário segue disponível mesmo com outro perfil em uso.
      avisos.push(
        `O perfil ${atual} continua disponível na rede de socorro, mas este celular só avisa de chamados novos quando você voltar a ele.`,
      );
    }
    return avisos.map((aviso) => `\n\n${aviso}`).join('');
  }

  /** `exibido` é o nome do entregador quando ele já chegou da API, ou o apelido. */
  function confirmarTroca(perfil: Perfil, exibido: string) {
    Alert.alert(
      'Trocar de perfil?',
      `Trocar para o perfil ${exibido}? O turno, os chamados e o histórico mostrados passam a ser deste perfil.${avisoAntesDeTrocar()}`,
      [
        { text: 'Cancelar', style: 'cancel' },
        { text: 'Trocar', onPress: () => void trocar(perfil) },
      ],
    );
  }

  async function trocar(perfil: Perfil) {
    await definirPerfilAtivo(perfil.entregadorId);
    const mensagem = `Você está usando o perfil ${perfil.apelido}`;
    AccessibilityInfo.announceForAccessibility(mensagem);
    // Volta para a aba Turno, que mostra o aviso da troca.
    // `troca` é um id único desta troca: a aba Turno mostra o aviso uma vez só por troca.
    // Só o apelido neutro vai na rota (nunca o nome, que é dado pessoal).
    router.dismissTo({ pathname: '/turno', params: { perfilTrocado: perfil.apelido, troca: novoIdDeTroca() } });
  }

  function confirmarRemocao(perfil: Perfil, exibido: string) {
    Alert.alert(
      'Remover deste aparelho?',
      `O perfil ${exibido} sai só deste celular: o cadastro continua na API e pode voltar pelo "Já tenho cadastro". Os turnos, alertas e ocorrências desse perfil salvos neste celular também serão apagados.`,
      [
        { text: 'Cancelar', style: 'cancel' },
        {
          text: 'Remover',
          style: 'destructive',
          onPress: () =>
            void removerPerfil(perfil.entregadorId).then(() =>
              AccessibilityInfo.announceForAccessibility(`Perfil ${perfil.apelido} removido deste aparelho`),
            ),
        },
      ],
    );
  }

  return (
    <Tela>
      <Texto>Cada perfil é um cadastro de entregador usado neste celular.</Texto>
      {perfis.map((perfil) => (
        <CartaoPerfil
          key={perfil.entregadorId}
          perfil={perfil}
          emUso={perfil.entregadorId === perfilAtivo?.entregadorId}
          aoTrocar={confirmarTroca}
          aoRemover={confirmarRemocao}
        />
      ))}
      <Botao
        titulo="Adicionar outro perfil"
        icone="person-add"
        aoPressionar={() => router.push('/cadastro')}
        accessibilityHint="Abre o cadastro de um novo entregador neste aparelho"
      />
    </Tela>
  );
}
