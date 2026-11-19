import { router } from 'expo-router';
import { useCallback, useEffect, useState } from 'react';
import { AccessibilityInfo } from 'react-native';

import { Aviso } from '@/componentes/Aviso';
import { Botao } from '@/componentes/Botao';
import {
  CamposTrocaTelefone,
  validarTrocaTelefone,
  type ErrosTrocaTelefone,
  type TelefonesDigitados,
} from '@/componentes/CamposTrocaTelefone';
import { Carregando } from '@/componentes/Carregando';
import { EstadoErro } from '@/componentes/EstadoErro';
import { EstadoVazio } from '@/componentes/EstadoVazio';
import { Tela } from '@/componentes/Tela';
import { useSessao } from '@/contextos/SessaoContext';
import { entregadorServico } from '@/servicos/entregadorServico';
import { converterErro } from '@/servicos/erros';
import { guardarParaProximaTela } from '@/utils/memoriaDoFluxo';
import { MENSAGEM_SEM_CONEXAO, mensagemComum } from '@/utils/mensagensErro';

/**
 * Troca do telefone do entregador (`PUT /entregadores/{id}/telefone`). Ao abrir, lê o telefone
 * mascarado do cadastro (`GET /entregadores/{id}`, só em memória, nunca pela rota). A pessoa
 * digita o telefone atual, conferido pelo DDD e pelos 4 últimos números (contra engano, não como
 * verificação de identidade), e o novo, com a regra da seção 3. O 200 traz o corpo do
 * `GET /entregadores/{id}`, que volta para o Perfil pela memória do fluxo. 409 telefone de outro
 * cadastro; 400 no campo.
 */
export default function Telefone() {
  const { contaAtiva } = useSessao();
  const [mascarado, setMascarado] = useState<string | null>(null);
  const [erroLeitura, setErroLeitura] = useState<string | null>(null);
  const [valores, setValores] = useState<TelefonesDigitados>({ atual: '', novo: '' });
  const [erros, setErros] = useState<ErrosTrocaTelefone>({});
  const [mensagem, setMensagem] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  const carregar = useCallback(async () => {
    if (!contaAtiva) {
      return;
    }
    try {
      setMascarado((await entregadorServico.obter(contaAtiva)).telefone);
      setErroLeitura(null);
    } catch (e) {
      const erro = converterErro(e);
      setErroLeitura(erro.tipo === 'sem-conexao' ? MENSAGEM_SEM_CONEXAO : mensagemComum(erro));
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

  if (!contaAtiva) {
    return (
      <Tela>
        <EstadoVazio titulo="Nenhuma conta neste aparelho" icone="person-outline" />
      </Tela>
    );
  }
  if (mascarado === null) {
    return erroLeitura ? (
      <Tela>
        <EstadoErro mensagem={erroLeitura} aoTentarDeNovo={() => void carregar()} />
      </Tela>
    ) : (
      <Carregando mensagem="Abrindo seu telefone..." />
    );
  }
  const entregadorId = contaAtiva;
  const telefoneMascarado = mascarado;

  async function salvar() {
    const novosErros = validarTrocaTelefone(valores, telefoneMascarado);
    setErros(novosErros);
    setMensagem(null);
    if (novosErros.atual || novosErros.novo) {
      return;
    }
    setEnviando(true);
    try {
      const atualizado = await entregadorServico.trocarTelefone(entregadorId, { telefone: valores.novo });
      // O Perfil usa estes dados (já mascarados) ao ganhar foco, sem consultar de novo.
      guardarParaProximaTela('entregadorAtualizado', atualizado);
      AccessibilityInfo.announceForAccessibility('Telefone trocado');
      router.back();
    } catch (e) {
      const erroApi = converterErro(e);
      if (erroApi.tipo === 'conflito') {
        setMensagem('Este telefone já está em outro cadastro.');
      } else if (erroApi.tipo === 'validacao') {
        setErros({ novo: 'Confira o telefone: 10 ou 11 números, com DDD.' });
      } else if (erroApi.tipo === 'sem-conexao') {
        setMensagem(`O telefone não foi trocado. ${MENSAGEM_SEM_CONEXAO}`);
      } else {
        setMensagem(mensagemComum(erroApi));
      }
    } finally {
      setEnviando(false);
    }
  }

  return (
    <Tela>
      <CamposTrocaTelefone mascarado={telefoneMascarado} valores={valores} aoMudar={setValores} erros={erros} />
      {mensagem ? <Aviso tipo="erro" mensagem={mensagem} /> : null}
      <Botao titulo="Salvar telefone" icone="save" aoPressionar={() => void salvar()} carregando={enviando} />
      <Botao titulo="Cancelar" variante="secundaria" aoPressionar={() => router.back()} desabilitado={enviando} />
    </Tela>
  );
}
