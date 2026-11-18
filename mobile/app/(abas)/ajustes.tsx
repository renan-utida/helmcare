import Constants from 'expo-constants';
import { router } from 'expo-router';
import { useState } from 'react';
import { Alert, Pressable } from 'react-native';

import { Aviso, type TipoAviso } from '@/componentes/Aviso';
import { Botao } from '@/componentes/Botao';
import { Campo } from '@/componentes/Campo';
import { Cartao } from '@/componentes/Cartao';
import { GrupoOpcoes } from '@/componentes/GrupoOpcoes';
import { LinhaInfo } from '@/componentes/LinhaInfo';
import { Tela } from '@/componentes/Tela';
import { Texto } from '@/componentes/Texto';
import { DESCRICAO_ORIGEM_URL, normalizarUrl } from '@/config/urlApi';
import { useSessao } from '@/contextos/SessaoContext';
import { PREFERENCIAS_TEMA, usePreferenciaTema, type PreferenciaTema } from '@/contextos/TemaContext';
import { converterErro } from '@/servicos/erros';
import { saudeServico } from '@/servicos/saudeServico';
import { invalidarAcessoParaTeste, invalidarRenovacaoParaTeste } from '@/servicos/testeSessaoDev';
import { MENSAGEM_ERRO_GENERICO } from '@/utils/mensagensErro';
import { validarUrlApi } from '@/utils/validacao';

/** Resultado do "Testar conexão", mostrado num aviso. */
interface ResultadoTeste {
  tipo: TipoAviso;
  mensagem: string;
}

/** Versão do app, do app.json. */
const VERSAO_APP = Constants.expoConfig?.version ?? 'desconhecida';

/** Texto de cada opção de aparência. */
const TEXTO_PREFERENCIA: Record<PreferenciaTema, string> = {
  sistema: 'Igual ao celular',
  claro: 'Claro',
  escuro: 'Escuro',
};

/**
 * Aba Ajustes: endereço da API (automático ou manual, salvo na tabela `preferencia`),
 * teste de conexão pelo `GET /saude`, aparência (tema) e o atalho para as contas do aparelho.
 */
export default function Ajustes() {
  const { urlApi, definirUrlManual, contaAtiva, sessao } = useSessao();
  const { preferencia, definirPreferencia } = usePreferenciaTema();
  const [urlDigitada, setUrlDigitada] = useState(urlApi.origem === 'manual' ? urlApi.url : '');
  const [erroUrl, setErroUrl] = useState<string | null>(null);
  const [resultado, setResultado] = useState<ResultadoTeste | null>(null);
  const [testando, setTestando] = useState(false);
  const [salvando, setSalvando] = useState(false);

  /** Ações de teste da renovação e do fim da sessão (só em __DEV__, pelo toque longo na versão). */
  function abrirTestesDaSessao() {
    if (!__DEV__ || !contaAtiva) {
      return;
    }
    Alert.alert('Testes da sessão (desenvolvimento)', 'Escolha o que invalidar na conta em uso.', [
      { text: 'Cancelar', style: 'cancel' },
      {
        text: 'Invalidar o token de acesso',
        onPress: () => void invalidarAcessoParaTeste(contaAtiva),
      },
      {
        text: 'Invalidar a renovação',
        style: 'destructive',
        onPress: () => void invalidarRenovacaoParaTeste(contaAtiva),
      },
    ]);
  }

  async function testarConexao() {
    setTestando(true);
    setResultado(null);
    try {
      await saudeServico.verificar();
      setResultado({ tipo: 'sucesso', mensagem: 'Conectado ao HelmCare' });
    } catch (e) {
      const erro = converterErro(e);
      if (erro.tipo === 'indisponivel') {
        setResultado({ tipo: 'atencao', mensagem: 'A API respondeu, mas o banco está fora' });
      } else if (erro.tipo === 'sem-conexao') {
        setResultado({
          tipo: 'erro',
          mensagem: 'Sem conexão com o computador da API: confira se o celular e o computador estão na mesma rede',
        });
      } else {
        setResultado({ tipo: 'erro', mensagem: MENSAGEM_ERRO_GENERICO });
      }
    } finally {
      setTestando(false);
    }
  }

  async function salvarUrl() {
    const erro = validarUrlApi(urlDigitada);
    setErroUrl(erro);
    if (erro) {
      return;
    }
    setSalvando(true);
    try {
      const url = normalizarUrl(urlDigitada);
      await definirUrlManual(url);
      setUrlDigitada(url);
      setResultado({ tipo: 'informacao', mensagem: 'Endereço salvo. Toque em Testar conexão para conferir.' });
    } finally {
      setSalvando(false);
    }
  }

  async function voltarAutomatica() {
    setSalvando(true);
    try {
      await definirUrlManual(null);
      setUrlDigitada('');
      setErroUrl(null);
      setResultado({ tipo: 'informacao', mensagem: 'O app voltou a achar o endereço sozinho.' });
    } finally {
      setSalvando(false);
    }
  }

  return (
    <Tela dentroDeAbas>
      <Cartao titulo="Conexão com a API">
        <LinhaInfo rotulo="Endereço em uso" valor={urlApi.url} selecionavel />
        <LinhaInfo rotulo="De onde veio" valor={DESCRICAO_ORIGEM_URL[urlApi.origem]} />
        <Botao titulo="Testar conexão" icone="pulse" aoPressionar={testarConexao} carregando={testando} />
        {resultado ? <Aviso tipo={resultado.tipo} mensagem={resultado.mensagem} /> : null}
      </Cartao>

      <Cartao titulo="Endereço manual">
        <Texto>Use só se o teste falhar. Coloque o IP do computador que roda a API.</Texto>
        <Campo
          rotulo="Endereço da API"
          dica="Exemplo: http://192.168.0.10:5000/api/v1"
          valor={urlDigitada}
          aoMudar={setUrlDigitada}
          tipo="url"
          erro={erroUrl}
          exemplo="http://192.168.0.10:5000/api/v1"
        />
        <Botao titulo="Salvar endereço" icone="save" aoPressionar={salvarUrl} carregando={salvando} />
        {urlApi.origem === 'manual' ? (
          <Botao
            titulo="Voltar à automática"
            variante="secundaria"
            aoPressionar={voltarAutomatica}
            desabilitado={salvando}
            accessibilityHint="Apaga o endereço salvo e deixa o app achar a API sozinho"
          />
        ) : null}
      </Cartao>

      <Cartao titulo="Aparência">
        <GrupoOpcoes
          rotulo="Cores do app"
          opcoes={PREFERENCIAS_TEMA}
          selecionada={preferencia}
          aoSelecionar={(opcao) => void definirPreferencia(opcao)}
          textoDe={(opcao) => TEXTO_PREFERENCIA[opcao]}
          colunas={1}
        />
      </Cartao>

      <Cartao titulo="Sobre o app">
        {/* Só no desenvolvimento, o toque longo na versão abre as ações de teste da sessão.
            Fora do __DEV__, a linha é só texto: nenhuma ação aparece. */}
        <Pressable
          onLongPress={__DEV__ ? abrirTestesDaSessao : undefined}
          accessibilityRole="text"
          accessibilityLabel={`Versão do app: ${VERSAO_APP}`}
        >
          <LinhaInfo rotulo="Versão do app" valor={VERSAO_APP} />
        </Pressable>
      </Cartao>

      <Cartao titulo="Contas neste aparelho">
        {contaAtiva ? <LinhaInfo rotulo="Em uso agora" valor={sessao?.nome ?? 'Conta salva'} /> : null}
        <Botao
          titulo="Ver e trocar contas"
          variante="secundaria"
          icone="people"
          aoPressionar={() => router.push('/contas')}
          accessibilityHint="Abre as contas salvas neste celular"
        />
      </Cartao>
    </Tela>
  );
}
