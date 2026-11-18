import { useCallback, useEffect, useState } from 'react';
import { View } from 'react-native';

import { Carregando } from '@/componentes/Carregando';
import { Cartao } from '@/componentes/Cartao';
import { EstadoErro } from '@/componentes/EstadoErro';
import { EstadoVazio } from '@/componentes/EstadoVazio';
import { Icone } from '@/componentes/Icone';
import { Tela } from '@/componentes/Tela';
import { Texto } from '@/componentes/Texto';
import { useSessao } from '@/contextos/SessaoContext';
import { useTema } from '@/contextos/TemaContext';
import { cofreServico } from '@/servicos/cofreServico';
import { converterErro, type ErroApi } from '@/servicos/erros';
import type { ItemAuditoriaCofreResposta, MotivoNegacaoCofre, OrigemAcessoCofre } from '@/tipos/contrato';
import { formatarDiaHora, formatarHoraComH } from '@/utils/datas';
import { MENSAGEM_SEM_CONEXAO, mensagemComum } from '@/utils/mensagensErro';

/** Origem do acesso em palavras. */
const TEXTO_ORIGEM: Record<OrigemAcessoCofre, string> = {
  SOS_ATIVO: 'Durante um SOS',
  EMERGENCIA_MANUAL: 'Emergência sem SOS ativo',
};

/** Motivo de uma negação em palavras. */
const TEXTO_MOTIVO: Record<MotivoNegacaoCofre, string> = {
  SOCORRISTA_NAO_VERIFICADO: 'Negado: socorrista não verificado',
  ACESSO_EXPIRADO: 'Negado: acesso expirado',
};

/** Resultado da tentativa: "Acesso liberado até 22h10" ou o motivo da negação. */
function textoResultado(item: ItemAuditoriaCofreResposta): string {
  if (item.concedido) {
    return `Acesso liberado até ${formatarHoraComH(item.expiraEm)}`;
  }
  return item.motivo ? TEXTO_MOTIVO[item.motivo] : 'Negado';
}

/** Uma tentativa de acesso à ficha, lida como uma frase só. O resultado tem ícone e texto. */
function CartaoAcesso({ item }: { item: ItemAuditoriaCofreResposta }) {
  const { cores, espaco } = useTema();
  const quando = formatarDiaHora(item.acessadoEm);
  const socorrista = `${item.socorrista.nome}, ${item.socorrista.organizacao}`;
  const origem = TEXTO_ORIGEM[item.origem];
  const resultado = textoResultado(item);
  return (
    <Cartao>
      <View
        accessible
        accessibilityLabel={`${quando}. Socorrista: ${socorrista}. ${origem}. ${resultado}.`}
        style={{ gap: espaco.pequeno }}
      >
        <Texto variante="legenda">{quando}</Texto>
        <Texto variante="rotulo">{item.socorrista.nome}</Texto>
        <Texto cor="textoSecundario">{item.socorrista.organizacao}</Texto>
        <Texto>{origem}</Texto>
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: espaco.pequeno }}>
          <Icone
            nome={item.concedido ? 'lock-open' : 'close-circle'}
            cor={item.concedido ? cores.aviso.sucesso.icone : cores.aviso.erro.icone}
          />
          <Texto variante="rotulo" style={{ flex: 1 }}>
            {resultado}
          </Texto>
        </View>
      </View>
    </Cartao>
  );
}

/**
 * Quem acessou minha ficha (`GET /cofre/auditoria/{entregadorId}`): um cartão por tentativa de um
 * socorrista, concedida ou negada, da mais recente para a mais antiga. Fica só na memória da tela
 * (nada no SQLite) e não traz dados de saúde. Puxar para atualizar consulta de novo.
 */
export default function AcessosFicha() {
  const { contaAtiva } = useSessao();
  const [itens, setItens] = useState<ItemAuditoriaCofreResposta[] | null>(null);
  const [erro, setErro] = useState<ErroApi | null>(null);
  const [atualizando, setAtualizando] = useState(false);

  const carregar = useCallback(async () => {
    if (!contaAtiva) {
      return;
    }
    try {
      const lista = await cofreServico.listarAuditoria(contaAtiva);
      // Da mais recente para a mais antiga, sem depender da ordem da API.
      setItens([...lista].sort((a, b) => b.acessadoEm.localeCompare(a.acessadoEm)));
      setErro(null);
    } catch (e) {
      setErro(converterErro(e));
    } finally {
      setAtualizando(false);
    }
  }, [contaAtiva]);

  useEffect(() => {
    let vivo = true;
    (async () => {
      // Espera um passo antes de mexer no estado (regra react-hooks/set-state-in-effect).
      await Promise.resolve();
      if (vivo) {
        await carregar();
      }
    })();
    return () => {
      vivo = false;
    };
  }, [carregar]);

  function atualizar() {
    setAtualizando(true);
    void carregar();
  }

  if (itens === null && !erro) {
    return <Carregando mensagem="Buscando quem acessou sua ficha..." />;
  }

  return (
    <Tela aoAtualizar={atualizar} atualizando={atualizando}>
      <Texto cor="textoSecundario">
        Cada vez que um socorrista tenta abrir sua ficha pelo QR Code do capacete, a tentativa aparece aqui, liberada
        ou negada.
      </Texto>
      {erro ? (
        <EstadoErro
          mensagem={erro.tipo === 'sem-conexao' ? MENSAGEM_SEM_CONEXAO : mensagemComum(erro)}
          aoTentarDeNovo={atualizar}
          tentando={atualizando}
        />
      ) : null}
      {itens !== null && itens.length === 0 ? (
        <EstadoVazio titulo="Ninguém acessou sua ficha até agora." icone="shield-checkmark-outline" />
      ) : null}
      {itens?.map((item) => (
        <CartaoAcesso key={`${item.acessadoEm}-${item.socorrista.nome}`} item={item} />
      ))}
    </Tela>
  );
}
