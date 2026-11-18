import { router } from 'expo-router';
import { useState } from 'react';
import { AccessibilityInfo, ActivityIndicator, View } from 'react-native';

import { Aviso, type TipoAviso } from '@/componentes/Aviso';
import { Botao } from '@/componentes/Botao';
import { Cartao } from '@/componentes/Cartao';
import { CartaoChamado } from '@/componentes/CartaoChamado';
import { Chave } from '@/componentes/Chave';
import { EstadoVazio } from '@/componentes/EstadoVazio';
import { GrupoOpcoes } from '@/componentes/GrupoOpcoes';
import { ParticiparRede } from '@/componentes/ParticiparRede';
import { SessaoIndisponivel } from '@/componentes/SessaoIndisponivel';
import { Tela } from '@/componentes/Tela';
import { Texto } from '@/componentes/Texto';
import { useChamados } from '@/contextos/ChamadosContext';
import { useSessao } from '@/contextos/SessaoContext';
import { useTema } from '@/contextos/TemaContext';
import { converterErro, type ErroApi } from '@/servicos/erros';
import { comecarPilhaEm } from '@/utils/navegacao';
import { formatarDecimal } from '@/utils/datas';
import { MENSAGEM_SEM_CONEXAO, mensagemComum } from '@/utils/mensagensErro';
import { IDS_POSICOES, posicaoSimulada, type IdPosicaoSimulada } from '@/utils/posicoesSimuladas';

/** Mensagem mostrada depois de uma ação ou de um erro. */
interface MensagemTela {
  tipo: TipoAviso;
  texto: string;
}

/** Mensagem dos erros do `PUT /voluntarios/{id}/disponibilidade`, pelo status HTTP. */
function mensagemDisponibilidade(erro: ErroApi): string {
  switch (erro.tipo) {
    case 'nao-encontrado':
      return 'Seu cadastro de voluntário não foi encontrado na API. Nada mudou.';
    case 'validacao':
      return 'A posição não foi aceita. Escolha uma das posições de novo.';
    case 'sem-conexao':
      return `Nada mudou. ${MENSAGEM_SEM_CONEXAO}`;
    default:
      return mensagemComum(erro);
  }
}

/** Mensagem do erro da consulta dos chamados, pelo status HTTP. */
function mensagemChamados(erro: ErroApi): string {
  if (erro.tipo === 'nao-encontrado') {
    return 'Seu cadastro de voluntário não foi encontrado na API.';
  }
  return erro.tipo === 'sem-conexao' ? MENSAGEM_SEM_CONEXAO : mensagemComum(erro);
}

/** Texto do botão de cada posição simulada, como "V1, MASP (cerca de 0,6 km da queda de teste)". */
function textoPosicao(id: IdPosicaoSimulada): string {
  const posicao = posicaoSimulada(id);
  return `${id}, ${posicao.nome} (cerca de ${formatarDecimal(posicao.distanciaEsperadaKm)} km da queda de teste)`;
}

/** Leitura de cada posição, com "quilômetros" por extenso. */
function leituraPosicao(id: IdPosicaoSimulada): string {
  const posicao = posicaoSimulada(id);
  return `${id}, ${posicao.nome}, cerca de ${formatarDecimal(posicao.distanciaEsperadaKm)} quilômetros da queda de teste`;
}

/**
 * Aba Chamados (rede de voluntários, seção 7). Sem `voluntarioId`: explicação e o botão para
 * participar. Com ele: a chave "Disponível para ajudar", a posição simulada (V1, V2 ou V3) e a
 * lista dos chamados próximos, consultada a cada 5 s pelo ChamadosContext com a disponibilidade
 * ligada. Nenhuma coordenada aparece antes do aceite.
 */
export default function Chamados() {
  const { cores, espaco } = useTema();
  const { contaAtiva, sessao } = useSessao();
  const {
    voluntarioId,
    rede,
    chamados,
    lidoEm,
    erroChamados,
    ocorrenciaAceita,
    definirDisponivel,
    escolherPosicao,
    aceitar,
    recarregarChamados,
  } = useChamados();
  const [enviando, setEnviando] = useState(false);
  const [aceitandoId, setAceitandoId] = useState<string | null>(null);
  const [mensagem, setMensagem] = useState<MensagemTela | null>(null);
  const [atualizando, setAtualizando] = useState(false);

  if (!contaAtiva) {
    return (
      <Tela dentroDeAbas>
        <EstadoVazio titulo="Nenhuma conta neste aparelho" icone="person-outline" />
        <Botao titulo="Entrar" aoPressionar={() => comecarPilhaEm('/login')} />
      </Tela>
    );
  }

  // Quem é voluntário vem da sessão: sem ela (carregando ou sem conexão), não dá para saber.
  if (!sessao) {
    return <SessaoIndisponivel />;
  }

  if (!voluntarioId) {
    return (
      <Tela dentroDeAbas>
        <Texto variante="titulo">Rede de socorro</Texto>
        <ParticiparRede />
      </Tela>
    );
  }

  async function alternarDisponivel() {
    setEnviando(true);
    setMensagem(null);
    const ligar = !rede.disponivel;
    try {
      const resultado = await definirDisponivel(ligar);
      if (resultado === 'sem-posicao') {
        setMensagem({ tipo: 'atencao', texto: 'Escolha onde você está para ficar disponível.' });
        return;
      }
      AccessibilityInfo.announceForAccessibility(
        ligar ? 'Você está disponível para ajudar' : 'Você não está mais disponível',
      );
    } catch (e) {
      setMensagem({ tipo: 'erro', texto: mensagemDisponibilidade(converterErro(e)) });
    } finally {
      setEnviando(false);
    }
  }

  async function escolher(id: IdPosicaoSimulada) {
    setEnviando(true);
    setMensagem(null);
    const ligava = !rede.disponivel && rede.posicao === null;
    try {
      await escolherPosicao(id);
      const nome = posicaoSimulada(id).nome;
      AccessibilityInfo.announceForAccessibility(
        ligava ? `Posição ${nome}. Você está disponível para ajudar` : `Posição ${nome}`,
      );
    } catch (e) {
      setMensagem({ tipo: 'erro', texto: mensagemDisponibilidade(converterErro(e)) });
    } finally {
      setEnviando(false);
    }
  }

  async function aceitarChamado(ocorrenciaId: string) {
    setAceitandoId(ocorrenciaId);
    setMensagem(null);
    try {
      await aceitar(ocorrenciaId);
      AccessibilityInfo.announceForAccessibility('Chamado aceito. Abrindo o atendimento.');
      router.push({ pathname: '/ocorrencia/[id]', params: { id: ocorrenciaId, papel: 'VOLUNTARIO' } });
    } catch (e) {
      const erro = converterErro(e);
      if (erro.tipo === 'conflito') {
        setMensagem({ tipo: 'atencao', texto: 'Este chamado já foi aceito ou você não pode atendê-lo agora.' });
        await recarregarChamados();
      } else if (erro.tipo === 'nao-encontrado') {
        setMensagem({ tipo: 'atencao', texto: 'Este chamado não existe mais. A lista foi atualizada.' });
        await recarregarChamados();
      } else {
        setMensagem({ tipo: 'erro', texto: mensagemComum(erro) });
      }
    } finally {
      setAceitandoId(null);
    }
  }

  async function atualizar() {
    setAtualizando(true);
    await recarregarChamados();
    setAtualizando(false);
  }

  return (
    <Tela dentroDeAbas aoAtualizar={rede.disponivel ? () => void atualizar() : undefined} atualizando={atualizando}>
      <Texto variante="titulo">Rede de socorro</Texto>

      {ocorrenciaAceita ? (
        <Cartao titulo="Atendimento em andamento">
          <Texto>Você aceitou um chamado e ele ainda não foi encerrado.</Texto>
          <Botao
            titulo="Abrir o atendimento"
            variante="perigo"
            icone="walk"
            aoPressionar={() =>
              router.push({ pathname: '/ocorrencia/[id]', params: { id: ocorrenciaAceita.id, papel: 'VOLUNTARIO' } })
            }
          />
        </Cartao>
      ) : null}

      <Cartao>
        <Chave
          rotulo="Disponível para ajudar"
          ligada={rede.disponivel}
          aoAlternar={() => void alternarDisponivel()}
          ocupada={enviando}
          descricao={rede.disponivel ? 'Sim: você recebe chamados perto de você' : 'Não: você não recebe chamados'}
          accessibilityHint={rede.posicao ? undefined : 'Escolha antes onde você está, nas opções abaixo'}
        />
        <GrupoOpcoes
          rotulo="Onde você está agora?"
          opcoes={IDS_POSICOES}
          selecionada={rede.posicao}
          aoSelecionar={(id) => void escolher(id)}
          textoDe={textoPosicao}
          leituraDe={leituraPosicao}
          colunas={1}
        />
        <Texto variante="legenda">
          Posição simulada para o teste: o app não usa o GPS do celular. A posição fica só na memória do app.
        </Texto>
      </Cartao>

      {mensagem ? <Aviso tipo={mensagem.tipo} mensagem={mensagem.texto} /> : null}

      {rede.disponivel ? (
        <View style={{ gap: espaco.medio }}>
          <Texto variante="subtitulo">Chamados perto de você</Texto>
          {erroChamados ? (
            <>
              <Aviso tipo="erro" mensagem={mensagemChamados(erroChamados)} />
              <Botao titulo="Tentar de novo" icone="refresh" variante="secundaria" aoPressionar={() => void atualizar()} />
            </>
          ) : null}
          {chamados === null && !erroChamados ? (
            <View
              accessible
              accessibilityRole="progressbar"
              accessibilityLabel="Procurando chamados perto de você"
              style={{ flexDirection: 'row', alignItems: 'center', gap: espaco.pequeno }}
            >
              <ActivityIndicator color={cores.primaria} />
              <Texto>Procurando chamados perto de você...</Texto>
            </View>
          ) : null}
          {chamados !== null && chamados.length === 0 ? (
            <EstadoVazio
              titulo="Nenhum chamado perto de você agora"
              mensagem="Obrigado por estar disponível."
              icone="checkmark-circle-outline"
            />
          ) : null}
          {chamados?.map((chamado) => (
            <CartaoChamado
              key={chamado.ocorrenciaId}
              chamado={chamado}
              lidoEm={lidoEm}
              aoAceitar={(id) => void aceitarChamado(id)}
              aceitando={aceitandoId === chamado.ocorrenciaId}
              bloqueado={aceitandoId !== null}
            />
          ))}
        </View>
      ) : (
        <Texto cor="textoSecundario">Ligue a disponibilidade para ver os chamados perto de você.</Texto>
      )}
    </Tela>
  );
}
