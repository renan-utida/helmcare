import { router, useFocusEffect } from 'expo-router';
import { useCallback, useState } from 'react';
import { View } from 'react-native';

import type { AlertaLocal } from '@/banco/alertaRepositorio';
import type { OcorrenciaLocal } from '@/banco/ocorrenciaRepositorio';
import type { TurnoLocal } from '@/banco/turnoRepositorio';
import { Botao } from '@/componentes/Botao';
import { Carregando } from '@/componentes/Carregando';
import { Cartao } from '@/componentes/Cartao';
import { situacaoDaMeta } from '@/componentes/CartaoCronometro';
import { EstadoVazio } from '@/componentes/EstadoVazio';
import { FaixaRisco, TEXTO_FAIXA } from '@/componentes/FaixaRisco';
import { Icone, type NomeIcone } from '@/componentes/Icone';
import { Tela } from '@/componentes/Tela';
import { Texto } from '@/componentes/Texto';
import { META_TEMPO_ACEITE_S } from '@/config/constantes';
import { useSessao } from '@/contextos/SessaoContext';
import { useTema } from '@/contextos/TemaContext';
import { resumirHistorico, useHistorico } from '@/hooks/useHistorico';
import type { EstadoDispositivo, EstadoOcorrencia } from '@/tipos/contrato';
import {
  duracaoFalada,
  formatarDia,
  formatarDiaHora,
  formatarDuracaoEntre,
  formatarHora,
  formatarMinutosSegundos,
} from '@/utils/datas';

/** Quantos itens cada seção mostra (os mais recentes). */
const LIMITE_POR_SECAO = 20;

/** Cada estado do capacete em linguagem simples, com ícone, para a seção Alertas. */
const TEXTO_ALERTA: Record<EstadoDispositivo, { texto: string; icone: NomeIcone }> = {
  DESARMADO: { texto: 'Proteção desligada', icone: 'shield-outline' },
  ARMADO: { texto: 'Proteção ligada', icone: 'shield-checkmark' },
  IMPACTO_DETECTADO: { texto: 'Impacto percebido pelo capacete', icone: 'alert-circle' },
  OBSERVACAO: { texto: 'Verificando se você estava bem', icone: 'alert-circle' },
  CONTAGEM: { texto: 'Contagem para cancelar o alerta', icone: 'timer' },
  SOS_ENVIADO: { texto: 'SOS enviado', icone: 'medkit' },
  CANCELADO: { texto: 'Alerta cancelado por você', icone: 'close-circle' },
  FALSO_POSITIVO: { texto: 'Alarme falso: você voltou a rodar', icone: 'checkmark-circle' },
};

/** Último estado salvo de cada ocorrência, em linguagem simples. */
const TEXTO_ESTADO_OCORRENCIA: Record<EstadoOcorrencia, string> = {
  SOS_CAMADA_1: 'Em andamento: chamando os primeiros voluntários',
  SOS_CAMADA_2: 'Em andamento: chamando toda a rede',
  SOCORRO_A_CAMINHO: 'Em andamento: voluntário a caminho',
  ENCERRADA: 'Encerrada',
  CANCELADA: 'SOS cancelado',
};

/** Tempo até o aceite com a meta, em texto e em leitura falada. */
function tempoAteAceite(segundos: number | null): { texto: string; leitura: string } {
  if (segundos === null) {
    return { texto: 'Tempo até o aceite: sem aceite', leitura: 'Tempo até o aceite: sem aceite' };
  }
  const meta = situacaoDaMeta(segundos, META_TEMPO_ACEITE_S) === 'dentro' ? 'dentro da meta' : 'acima da meta';
  return {
    texto: `Tempo até o aceite: ${formatarMinutosSegundos(segundos)}, ${meta}`,
    leitura: `Tempo até o aceite: ${duracaoFalada(segundos)}, ${meta}`,
  };
}

/** Título de uma seção, lido como cabeçalho, com a contagem quando a lista foi cortada. */
function TituloSecao({ titulo, total }: { titulo: string; total: number }) {
  return (
    <View>
      <Texto variante="subtitulo">{titulo}</Texto>
      {total > LIMITE_POR_SECAO ? (
        <Texto variante="legenda">{`Mostrando os ${LIMITE_POR_SECAO} mais recentes de ${total}.`}</Texto>
      ) : null}
    </View>
  );
}

/** Um turno: data, duração, Índice com o selo da faixa e a proteção. Lido como uma frase só. */
function ItemTurno({ turno }: { turno: TurnoLocal }) {
  const { espaco } = useTema();
  const duracao = turno.fim ? formatarDuracaoEntre(turno.inicio, turno.fim) : null;
  const protecao = turno.protecaoAtiva ? 'Proteção contra quedas ligada' : 'Proteção contra quedas desligada';
  const duracaoLida = turno.fim ? formatarDuracaoEntre(turno.inicio, turno.fim, true) : null;
  const leitura = `Turno de ${formatarDiaHora(turno.inicio)}. ${
    duracaoLida ? `Duração: ${duracaoLida}` : 'Ainda em andamento'
  }. Índice ${turno.indiceRisco} de 100, ${TEXTO_FAIXA[turno.faixaRisco]}. ${protecao}.`;
  return (
    <Cartao>
      <View accessible accessibilityLabel={leitura} style={{ gap: espaco.pequeno }}>
        <Texto variante="rotulo">{formatarDiaHora(turno.inicio)}</Texto>
        <Texto>{duracao ? `Duração: ${duracao}` : 'Ainda em andamento'}</Texto>
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', gap: espaco.pequeno }}>
          <Texto variante="rotulo">{`Índice ${turno.indiceRisco}`}</Texto>
          <FaixaRisco faixa={turno.faixaRisco} />
        </View>
        <Texto cor="textoSecundario">{protecao}</Texto>
      </View>
    </Cartao>
  );
}

/** Uma mudança de estado do capacete: data, hora e o estado em linguagem simples. */
function ItemAlerta({ alerta }: { alerta: AlertaLocal }) {
  const { cores, espaco } = useTema();
  const { texto, icone } = TEXTO_ALERTA[alerta.estado];
  const quando = `${formatarDia(alerta.registradoEm)} às ${formatarHora(alerta.registradoEm)}`;
  return (
    <View
      accessible
      accessibilityLabel={`${quando}: ${texto}`}
      style={{ flexDirection: 'row', alignItems: 'center', gap: espaco.medio, paddingVertical: espaco.pequeno }}
    >
      <Icone nome={icone} cor={cores.textoSecundario} />
      <View style={{ flex: 1 }}>
        <Texto variante="legenda">{quando}</Texto>
        <Texto variante="rotulo">{texto}</Texto>
      </View>
    </View>
  );
}

/** Uma ocorrência: data, papel, estado final e o tempo até o aceite com a meta. */
function ItemOcorrencia({ ocorrencia }: { ocorrencia: OcorrenciaLocal }) {
  const { cores, espaco } = useTema();
  const papel = ocorrencia.papel === 'VITIMA' ? 'Você pediu socorro' : 'Você ajudou';
  const estado = TEXTO_ESTADO_OCORRENCIA[ocorrencia.estado];
  const tempo = tempoAteAceite(ocorrencia.tempoAteAceiteSegundos);
  return (
    <Cartao>
      <View
        accessible
        accessibilityLabel={`${formatarDiaHora(ocorrencia.criadaEm)}. ${papel}. ${estado}. ${tempo.leitura}.`}
        style={{ gap: espaco.pequeno }}
      >
        <Texto variante="legenda">{formatarDiaHora(ocorrencia.criadaEm)}</Texto>
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: espaco.pequeno }}>
          <Icone nome={ocorrencia.papel === 'VITIMA' ? 'medkit' : 'walk'} cor={cores.textoSecundario} />
          <Texto variante="rotulo" style={{ flex: 1 }}>
            {papel}
          </Texto>
        </View>
        <Texto>{estado}</Texto>
        <Texto>{tempo.texto}</Texto>
      </View>
    </Cartao>
  );
}

/**
 * Aba Histórico: lê só o SQLite do perfil ativo (funciona com a API desligada). No topo, o
 * resumo com o indicador de impacto do HelmCare (tempo médio até o aceite, meta de 2 minutos);
 * depois as seções Turnos, Alertas e Ocorrências. "Puxar para atualizar" relê o SQLite.
 */
export default function Historico() {
  const { cores, espaco } = useTema();
  const { perfilAtivo } = useSessao();
  const { dados, recarregar } = useHistorico(perfilAtivo?.entregadorId ?? null);
  const [atualizando, setAtualizando] = useState(false);

  // Relê o SQLite toda vez que a aba ganha foco (um turno ou uma ocorrência pode ter terminado).
  useFocusEffect(
    useCallback(() => {
      void recarregar();
    }, [recarregar]),
  );

  async function atualizar() {
    setAtualizando(true);
    await recarregar();
    setAtualizando(false);
  }

  if (!perfilAtivo) {
    return (
      <Tela dentroDeAbas>
        <EstadoVazio titulo="Nenhum perfil neste aparelho" icone="person-outline" />
        <Botao titulo="Fazer cadastro" aoPressionar={() => router.push('/cadastro')} />
      </Tela>
    );
  }
  if (!dados) {
    return <Carregando mensagem="Abrindo o histórico..." />;
  }

  const resumo = resumirHistorico(dados);
  const media = resumo.mediaAceiteSegundos;
  const situacaoMedia = media === null ? null : situacaoDaMeta(media, META_TEMPO_ACEITE_S);
  const leituraResumo = `Resumo. ${resumo.turnos} ${resumo.turnos === 1 ? 'turno registrado' : 'turnos registrados'}. ${
    resumo.ocorrencias
  } ${resumo.ocorrencias === 1 ? 'ocorrência' : 'ocorrências'}. ${
    media === null
      ? 'Ainda sem ocorrência com aceite.'
      : `Tempo médio até o aceite: ${duracaoFalada(media)}, ${situacaoMedia === 'dentro' ? 'dentro' : 'acima'} da meta de ${META_TEMPO_ACEITE_S / 60} minutos.`
  }`;

  return (
    <Tela dentroDeAbas aoAtualizar={() => void atualizar()} atualizando={atualizando}>
      <Texto variante="titulo">Seu histórico</Texto>
      <Texto cor="textoSecundario">Salvo neste celular: funciona mesmo sem internet.</Texto>

      <Cartao titulo="Resumo">
        <View accessible accessibilityLabel={leituraResumo} style={{ gap: espaco.pequeno }}>
          <Texto>{`Turnos registrados: ${resumo.turnos}`}</Texto>
          <Texto>{`Ocorrências: ${resumo.ocorrencias}`}</Texto>
          {media === null ? (
            <Texto>Tempo médio até o aceite: ainda sem ocorrência com aceite.</Texto>
          ) : (
            <>
              <Texto variante="rotulo">{`Tempo médio até o aceite: ${formatarMinutosSegundos(media)}`}</Texto>
              <View style={{ flexDirection: 'row', alignItems: 'center', gap: espaco.pequeno }}>
                <Icone
                  nome={situacaoMedia === 'dentro' ? 'checkmark-circle' : 'time'}
                  cor={situacaoMedia === 'dentro' ? cores.aviso.sucesso.icone : cores.aviso.atencao.icone}
                />
                <Texto style={{ flex: 1 }}>
                  {situacaoMedia === 'dentro' ? 'Dentro da meta' : 'Acima da meta'}
                </Texto>
              </View>
            </>
          )}
          <Texto variante="legenda">
            {`É o indicador de impacto do HelmCare: socorro aceito em até ${META_TEMPO_ACEITE_S / 60} minutos depois da queda.`}
          </Texto>
        </View>
      </Cartao>

      <View style={{ gap: espaco.medio }}>
        <TituloSecao titulo="Turnos" total={dados.turnos.length} />
        {dados.turnos.length === 0 ? (
          <EstadoVazio titulo="Nenhum turno ainda" mensagem="Os turnos que você começar aparecem aqui." icone="speedometer-outline" />
        ) : (
          dados.turnos.slice(0, LIMITE_POR_SECAO).map((turno) => <ItemTurno key={turno.id} turno={turno} />)
        )}
      </View>

      <View style={{ gap: espaco.medio }}>
        <TituloSecao titulo="Alertas" total={dados.alertas.length} />
        {dados.alertas.length === 0 ? (
          <EstadoVazio
            titulo="Nenhum alerta ainda"
            mensagem="As mudanças do capacete durante os turnos com proteção aparecem aqui."
            icone="shield-outline"
          />
        ) : (
          <Cartao>
            {dados.alertas.slice(0, LIMITE_POR_SECAO).map((alerta) => (
              <ItemAlerta key={alerta.id} alerta={alerta} />
            ))}
          </Cartao>
        )}
      </View>

      <View style={{ gap: espaco.medio }}>
        <TituloSecao titulo="Ocorrências" total={dados.ocorrencias.length} />
        {dados.ocorrencias.length === 0 ? (
          <EstadoVazio
            titulo="Nenhuma ocorrência ainda"
            mensagem="Os pedidos de socorro e os chamados que você atendeu aparecem aqui."
            icone="medkit-outline"
          />
        ) : (
          dados.ocorrencias
            .slice(0, LIMITE_POR_SECAO)
            .map((ocorrencia) => <ItemOcorrencia key={`${ocorrencia.id}-${ocorrencia.papel}`} ocorrencia={ocorrencia} />)
        )}
      </View>
    </Tela>
  );
}
