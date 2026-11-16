import { router } from 'expo-router';
import { useState } from 'react';
import { View } from 'react-native';

import { Aviso, type TipoAviso } from '@/componentes/Aviso';
import { Botao } from '@/componentes/Botao';
import { Campo } from '@/componentes/Campo';
import { Cartao } from '@/componentes/Cartao';
import { LinhaInfo } from '@/componentes/LinhaInfo';
import { Tela } from '@/componentes/Tela';
import { Texto } from '@/componentes/Texto';
import { DESCRICAO_ORIGEM_URL, normalizarUrl } from '@/config/urlApi';
import { useSessao } from '@/contextos/SessaoContext';
import { useTema } from '@/contextos/TemaContext';
import { converterErro } from '@/servicos/erros';
import { saudeServico } from '@/servicos/saudeServico';
import { MENSAGEM_ERRO_GENERICO } from '@/utils/mensagensErro';
import { validarUrlApi } from '@/utils/validacao';

/** Resultado do "Testar conexão", mostrado num aviso. */
interface ResultadoTeste {
  tipo: TipoAviso;
  mensagem: string;
}

/**
 * Configurações: endereço da API (automático ou manual, salvo na tabela `preferencia`),
 * teste de conexão pelo `GET /saude` e troca de perfil do aparelho.
 */
export default function Configuracoes() {
  const { espaco } = useTema();
  const { urlApi, definirUrlManual, perfis, perfilAtivo, definirPerfilAtivo } = useSessao();
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

      <Cartao titulo="Perfis neste aparelho">
        {perfis.map((perfil) => {
          const emUso = perfil.entregadorId === perfilAtivo?.entregadorId;
          return (
            <View key={perfil.entregadorId} style={{ gap: espaco.pequeno }}>
              <LinhaInfo
                rotulo={emUso ? 'Em uso agora' : 'Perfil'}
                valor={`${perfil.apelido}${perfil.codigoCapacete ? ` (${perfil.codigoCapacete})` : ''}`}
              />
              {emUso ? null : (
                <Botao
                  titulo="Usar este perfil"
                  variante="secundaria"
                  accessibilityLabel={`Usar o perfil ${perfil.apelido}`}
                  aoPressionar={() => void definirPerfilAtivo(perfil.entregadorId)}
                />
              )}
            </View>
          );
        })}
        <Botao
          titulo="Adicionar outro perfil"
          icone="person-add"
          aoPressionar={() => router.push('/cadastro')}
          accessibilityHint="Abre o cadastro de um novo entregador neste aparelho"
        />
      </Cartao>
    </Tela>
  );
}
