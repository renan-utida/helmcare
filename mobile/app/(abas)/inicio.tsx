import { router, useFocusEffect, useLocalSearchParams } from 'expo-router';
import { useCallback, useState } from 'react';
import { Pressable, View } from 'react-native';

import { Aviso } from '@/componentes/Aviso';
import { Botao } from '@/componentes/Botao';
import { Cartao } from '@/componentes/Cartao';
import { CartaoClima } from '@/componentes/CartaoClima';
import { EstadoVazio } from '@/componentes/EstadoVazio';
import { useAparenciaCapacete } from '@/componentes/EstadoCapacete';
import { FaixaRisco, TEXTO_FAIXA } from '@/componentes/FaixaRisco';
import { Icone, type NomeIcone } from '@/componentes/Icone';
import { Tela } from '@/componentes/Tela';
import { Texto } from '@/componentes/Texto';
import { useSessao } from '@/contextos/SessaoContext';
import { useTema } from '@/contextos/TemaContext';
import { useTurno, type TurnoAtual } from '@/contextos/TurnoContext';
import { useClima } from '@/hooks/useClima';
import { formatarDiaHora } from '@/utils/datas';
import { comecarPilhaEm } from '@/utils/navegacao';

/**
 * Id da última troca de conta já avisada nesta aba. Fica fora do componente para valer
 * mesmo se a aba for recriada; só guarda um número de controle, nenhum dado pessoal.
 */
let ultimaTrocaMostrada: string | null = null;

/**
 * Saudação pelo horário do aparelho: "Bom dia" das 5h às 11h59, "Boa tarde" das 12h às 17h59 e
 * "Boa noite" das 18h às 4h59.
 */
function saudacaoDoHorario(hora: number): string {
  if (hora >= 5 && hora < 12) return 'Bom dia';
  if (hora >= 12 && hora < 18) return 'Boa tarde';
  return 'Boa noite';
}

/** Primeiro nome do nome completo da sessão (só em memória). */
function primeiroNome(nome: string): string {
  return nome.trim().split(/\s+/)[0] ?? nome;
}

/** Um atalho da Início: cartão grande com ícone num círculo tingido e o nome. */
function Atalho({
  titulo,
  icone,
  aoPressionar,
  dica,
}: {
  titulo: string;
  icone: NomeIcone;
  aoPressionar: () => void;
  dica: string;
}) {
  const { cores, espaco, raio, medidas } = useTema();
  return (
    <Pressable
      onPress={aoPressionar}
      accessibilityRole="button"
      accessibilityLabel={titulo}
      accessibilityHint={dica}
      style={({ pressed }) => ({
        flexBasis: '45%',
        flexGrow: 1,
        minHeight: medidas.alturaBotao * 2,
        backgroundColor: cores.superficie,
        borderRadius: raio.medio,
        padding: espaco.medio,
        gap: espaco.pequeno,
        justifyContent: 'center',
        opacity: pressed ? 0.85 : 1,
      })}
    >
      <View
        style={{
          width: medidas.toqueMinimo,
          height: medidas.toqueMinimo,
          borderRadius: medidas.toqueMinimo / 2,
          backgroundColor: cores.aviso.informacao.fundo,
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        <Icone nome={icone} cor={cores.aviso.informacao.icone} />
      </View>
      <Texto variante="rotulo">{titulo}</Texto>
    </Pressable>
  );
}

/** Cartão do turno com turno ativo: o resumo e o toque que abre o Turno em andamento. */
function ResumoTurno({ turno }: { turno: TurnoAtual }) {
  const { cores, espaco, raio } = useTema();
  const { alerta, emergencia } = useTurno();
  const capacete = useAparenciaCapacete(turno.protecaoAtiva !== false ? (alerta?.estadoCapacete ?? null) : null);
  const horarios =
    turno.inicio && turno.fimPrevisto
      ? `Começou ${formatarDiaHora(turno.inicio)}. Fim previsto ${formatarDiaHora(turno.fimPrevisto)}.`
      : 'Aberto antes neste ou em outro celular.';
  const indice = turno.indiceDisponivel ? `Índice ${turno.indiceRisco} de 100, ${TEXTO_FAIXA[turno.faixaRisco].toLowerCase()}.` : '';
  return (
    <View style={{ gap: espaco.medio }}>
      <Pressable
        onPress={() => router.push('/turno/ativo')}
        accessibilityRole="button"
        accessibilityLabel={`Turno em andamento. Seu capacete: ${capacete.texto}. ${indice} ${horarios}`}
        accessibilityHint="Abre o turno em andamento"
        style={({ pressed }) => ({
          backgroundColor: cores.superficie,
          borderRadius: raio.medio,
          padding: espaco.medio,
          gap: espaco.medio,
          opacity: pressed ? 0.85 : 1,
        })}
      >
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: espaco.pequeno }}>
          <Texto variante="subtitulo" style={{ flex: 1 }}>
            Turno em andamento
          </Texto>
          <Icone nome="chevron-forward" cor={cores.textoSecundario} />
        </View>
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: espaco.pequeno }}>
          <Icone nome={capacete.icone} cor={capacete.cor} tamanho={32} />
          <Texto variante="rotulo" style={{ flex: 1 }}>
            {capacete.texto}
          </Texto>
        </View>
        {turno.indiceDisponivel ? (
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', gap: espaco.pequeno }}>
            <FaixaRisco faixa={turno.faixaRisco} />
            <Texto variante="rotulo">{`Índice ${turno.indiceRisco}`}</Texto>
          </View>
        ) : null}
        <Texto cor="textoSecundario">{horarios}</Texto>
      </Pressable>
      {emergencia ? (
        <Botao
          titulo={emergencia.tipo === 'alerta' ? 'Abrir o alerta de queda' : 'Abrir a ocorrência'}
          variante="perigo"
          icone="alert-circle"
          aoPressionar={() =>
            emergencia.tipo === 'alerta'
              ? router.push('/alerta')
              : router.push({ pathname: '/ocorrencia/[id]', params: { id: emergencia.ocorrenciaId, papel: 'VITIMA' } })
          }
        />
      ) : null}
    </View>
  );
}

/**
 * Aba Início: a saudação com o primeiro nome (da sessão, só em memória; sem conexão, sem nome),
 * o cartão "Clima agora" (`GET /clima`, ao abrir a aba e ao puxar para atualizar, fora do
 * polling), o cartão do turno (começar ou o resumo do turno em andamento) e os atalhos.
 */
export default function Inicio() {
  const { espaco } = useTema();
  const { contaAtiva, sessao } = useSessao();
  const { turno, carregando } = useTurno();
  const { clima, atualizar: atualizarClima } = useClima();
  const [atualizando, setAtualizando] = useState(false);
  // Id único da troca de conta feita em Contas (pela rota). Nenhum dado pessoal vai na rota.
  const { troca } = useLocalSearchParams<{ troca?: string }>();
  const [trocaVisivel, setTrocaVisivel] = useState(false);
  // Hora do aparelho para a saudação, relida a cada vez que a aba ganha foco.
  const [hora, setHora] = useState(() => new Date().getHours());

  // Ao ganhar o foco: consulta o clima e mostra o aviso da troca de conta uma vez só por troca
  // (ao sair da aba, ele some; ao voltar, a troca já está marcada e não reaparece).
  useFocusEffect(
    useCallback(() => {
      setHora(new Date().getHours());
      void atualizarClima();
      if (troca && troca !== ultimaTrocaMostrada) {
        ultimaTrocaMostrada = troca;
        setTrocaVisivel(true);
      }
      return () => setTrocaVisivel(false);
    }, [atualizarClima, troca]),
  );

  async function atualizar() {
    setAtualizando(true);
    await atualizarClima();
    setAtualizando(false);
  }

  if (!contaAtiva) {
    return (
      <Tela dentroDeAbas>
        <EstadoVazio titulo="Nenhuma conta neste aparelho" icone="person-outline" />
        <Botao titulo="Entrar" aoPressionar={() => comecarPilhaEm('/login')} />
      </Tela>
    );
  }

  const saudacao = saudacaoDoHorario(hora);
  const avisoTroca = trocaVisivel ? (sessao ? `Você está usando a conta de ${sessao.nome}` : 'Você trocou de conta.') : null;

  return (
    <Tela dentroDeAbas aoAtualizar={() => void atualizar()} atualizando={atualizando}>
      {avisoTroca ? <Aviso tipo="informacao" mensagem={avisoTroca} compacto /> : null}
      <Texto variante="titulo">{sessao ? `${saudacao}, ${primeiroNome(sessao.nome)}!` : `${saudacao}!`}</Texto>

      {clima ? <CartaoClima clima={clima} /> : null}

      {carregando ? (
        <Texto cor="textoSecundario">Abrindo o turno...</Texto>
      ) : turno ? (
        <ResumoTurno turno={turno} />
      ) : (
        <Cartao>
          <Texto variante="subtitulo">Vamos começar seu turno de hoje?</Texto>
          <Texto cor="textoSecundario">Veja o risco do seu horário e ligue a proteção do capacete.</Texto>
          <Botao titulo="Começar turno" icone="play" aoPressionar={() => router.push('/turno/novo')} />
        </Cartao>
      )}

      <View style={{ gap: espaco.medio }}>
        <Texto variante="subtitulo">Atalhos</Texto>
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: espaco.medio }}>
          <Atalho titulo="Histórico" icone="time" dica="Abre seus turnos, alertas e ocorrências" aoPressionar={() => router.navigate('/historico')} />
          <Atalho
            titulo="Contatos de emergência"
            icone="people-circle"
            dica="Abre o Perfil nos seus contatos"
            aoPressionar={() => router.navigate({ pathname: '/perfil', params: { secao: 'contatos', pedido: String(Date.now()) } })}
          />
          <Atalho titulo="Ficha médica" icone="medkit" dica="Abre a sua ficha médica" aoPressionar={() => router.push('/ficha-medica')} />
          <Atalho titulo="Rede de socorro" icone="people" dica="Abre os chamados da rede" aoPressionar={() => router.navigate('/chamados')} />
        </View>
      </View>
    </Tela>
  );
}
