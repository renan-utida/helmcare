import { router } from 'expo-router';
import { AccessibilityInfo, Alert, View } from 'react-native';

import type { Perfil } from '@/banco/perfilRepositorio';
import { Botao } from '@/componentes/Botao';
import { Cartao } from '@/componentes/Cartao';
import { Icone } from '@/componentes/Icone';
import { LinhaInfo } from '@/componentes/LinhaInfo';
import { Tela } from '@/componentes/Tela';
import { Texto } from '@/componentes/Texto';
import { useSessao } from '@/contextos/SessaoContext';
import { useTema } from '@/contextos/TemaContext';
import { useTurno } from '@/contextos/TurnoContext';

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

/**
 * Perfis neste aparelho: um cartão por perfil, troca com confirmação (e aviso de turno aberto)
 * e remoção só do celular. Aberta pelo cabeçalho das abas e pelas Configurações.
 */
export default function Perfis() {
  const { perfis, perfilAtivo, definirPerfilAtivo, removerPerfil } = useSessao();
  const { turno } = useTurno();

  /** Avisos sobre o perfil atual que a pessoa precisa saber antes de trocar. */
  function avisoAntesDeTrocar(): string {
    if (turno) {
      return `\n\nAtenção: o perfil ${perfilAtivo?.apelido ?? 'atual'} tem um turno aberto. Ele continua aberto e volta a aparecer quando você usar esse perfil de novo.`;
    }
    return '';
  }

  function confirmarTroca(perfil: Perfil) {
    Alert.alert(
      'Trocar de perfil?',
      `Trocar para o perfil ${perfil.apelido}? O turno, os chamados e o histórico mostrados passam a ser deste perfil.${avisoAntesDeTrocar()}`,
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
    router.dismissTo({ pathname: '/turno', params: { perfilTrocado: perfil.apelido } });
  }

  function confirmarRemocao(perfil: Perfil) {
    Alert.alert(
      'Remover deste aparelho?',
      `O perfil ${perfil.apelido} sai só deste celular: o cadastro continua na API e pode voltar pelo "Já tenho cadastro". Os turnos, alertas e ocorrências desse perfil salvos neste celular também serão apagados.`,
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
      {perfis.map((perfil) => {
        const emUso = perfil.entregadorId === perfilAtivo?.entregadorId;
        return (
          <Cartao key={perfil.entregadorId} titulo={perfil.apelido}>
            {emUso ? <SeloEmUso /> : null}
            <LinhaInfo rotulo="Capacete" valor={perfil.codigoCapacete ?? 'Sem capacete'} />
            <LinhaInfo rotulo="Voluntário da rede" valor={perfil.voluntarioId ? 'Sim' : 'Não'} />
            {emUso ? null : (
              <>
                <Botao
                  titulo="Trocar para este perfil"
                  icone="swap-horizontal"
                  aoPressionar={() => confirmarTroca(perfil)}
                  accessibilityLabel={`Trocar para o perfil ${perfil.apelido}`}
                  accessibilityHint="Pede confirmação antes de trocar"
                />
                <Botao
                  titulo="Remover deste aparelho"
                  variante="secundaria"
                  icone="trash"
                  aoPressionar={() => confirmarRemocao(perfil)}
                  accessibilityLabel={`Remover o perfil ${perfil.apelido} deste aparelho`}
                  accessibilityHint="Apaga só do celular. O cadastro continua na API."
                />
              </>
            )}
          </Cartao>
        );
      })}
      <Botao
        titulo="Adicionar outro perfil"
        icone="person-add"
        aoPressionar={() => router.push('/cadastro')}
        accessibilityHint="Abre o cadastro de um novo entregador neste aparelho"
      />
    </Tela>
  );
}
