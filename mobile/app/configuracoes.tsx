import { router } from 'expo-router';
import { useState } from 'react';

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
import { MENSAGEM_ERRO_GENERICO } from '@/utils/mensagensErro';
import { validarUrlApi } from '@/utils/validacao';

/** Resultado do "Testar conexão", mostrado num aviso. */
interface ResultadoTeste {
  tipo: TipoAviso;
  mensagem: string;
}

/** Texto de cada opção de aparência. */
const TEXTO_PREFERENCIA: Record<PreferenciaTema, string> = {
  sistema: 'Igual ao celular',
  claro: 'Claro',
  escuro: 'Escuro',
};

/**
 * Configurações: endereço da API (automático ou manual, salvo na tabela `preferencia`),
 * teste de conexão pelo `GET /saude`, aparência (tema) e o atalho para os perfis do aparelho.
 */
export default function Configuracoes() {
  const { urlApi, definirUrlManual, perfilAtivo } = useSessao();
  const { preferencia, definirPreferencia } = usePreferenciaTema();
  const [urlDigitada, setUrlDigitada] = useState(urlApi.origem === 'manual' ? urlApi.url : '');
  const [erroUrl, setErroUrl] = useState<string | null>(null);
  const [resultado, setResultado] = useState<ResultadoTeste | null>(null);
  const [testando, setTestando] = useState(false);
  const [salvando, setSalvando] = useState(false);

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
    <Tela>
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

      <Cartao titulo="Perfis neste aparelho">
        {perfilAtivo ? <LinhaInfo rotulo="Em uso agora" valor={perfilAtivo.apelido} /> : null}
        <Botao
          titulo="Ver e trocar perfis"
          variante="secundaria"
          icone="people"
          aoPressionar={() => router.push('/perfis')}
          accessibilityHint="Abre a lista de perfis deste aparelho"
        />
      </Cartao>
    </Tela>
  );
}
