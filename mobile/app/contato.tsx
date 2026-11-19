import { router, Stack, useLocalSearchParams } from 'expo-router';
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
import { Cartao } from '@/componentes/Cartao';
import { EstadoErro } from '@/componentes/EstadoErro';
import { EstadoVazio } from '@/componentes/EstadoVazio';
import { FormularioContato } from '@/componentes/FormularioContato';
import { LinhaInfo } from '@/componentes/LinhaInfo';
import { Tela } from '@/componentes/Tela';
import { Texto } from '@/componentes/Texto';
import { useSessao } from '@/contextos/SessaoContext';
import { entregadorServico } from '@/servicos/entregadorServico';
import { converterErro } from '@/servicos/erros';
import type { ContatoResposta } from '@/tipos/contrato';
import { MENSAGEM_SEM_CONEXAO, mensagemComum } from '@/utils/mensagensErro';

/** Contato a editar, como veio do `GET /entregadores/{id}` (telefone mascarado), só em memória. */
type Edicao =
  | { tipo: 'carregando' }
  | { tipo: 'pronto'; contato: ContatoResposta }
  | { tipo: 'nao-encontrado' }
  | { tipo: 'erro'; mensagem: string };

/**
 * Edição de um contato de emergência: só o telefone muda. O nome e o parentesco aparecem para
 * leitura, e o "Telefone atual" é conferido pelo DDD e pelos 4 últimos números do telefone
 * mascarado do contato (contra engano). O `PUT` continua com o corpo inteiro: o nome e o
 * parentesco atuais, como vieram do `GET`, e o telefone novo.
 */
function EditarContato({ entregadorId, contato }: { entregadorId: string; contato: ContatoResposta }) {
  const [valores, setValores] = useState<TelefonesDigitados>({ atual: '', novo: '' });
  const [erros, setErros] = useState<ErrosTrocaTelefone>({});
  const [mensagem, setMensagem] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  async function salvar() {
    const novosErros = validarTrocaTelefone(valores, contato.telefone);
    setErros(novosErros);
    setMensagem(null);
    if (novosErros.atual || novosErros.novo) {
      return;
    }
    setEnviando(true);
    try {
      await entregadorServico.editarContato(entregadorId, contato.id, {
        nome: contato.nome,
        parentesco: contato.parentesco,
        telefone: valores.novo,
      });
      AccessibilityInfo.announceForAccessibility('Telefone do contato trocado');
      router.back();
    } catch (e) {
      const erroApi = converterErro(e);
      if (erroApi.tipo === 'nao-encontrado') {
        setMensagem('Este contato não existe mais. Volte ao Perfil para ver a lista atualizada.');
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
      <Cartao>
        <LinhaInfo rotulo="Nome" valor={contato.nome} />
        <LinhaInfo rotulo="Parentesco" valor={contato.parentesco} />
        <Texto variante="legenda">Para trocar o nome ou o parentesco, exclua o contato e adicione de novo.</Texto>
      </Cartao>
      <CamposTrocaTelefone
        mascarado={contato.telefone}
        valores={valores}
        aoMudar={setValores}
        erros={erros}
        rotuloCadastro="Telefone do contato no cadastro"
      />
      {mensagem ? <Aviso tipo="erro" mensagem={mensagem} /> : null}
      <Botao titulo="Salvar telefone" icone="save" aoPressionar={() => void salvar()} carregando={enviando} />
      <Botao titulo="Cancelar" variante="secundaria" aoPressionar={() => router.back()} desabilitado={enviando} />
    </Tela>
  );
}

/**
 * Modal do contato de emergência. Sem parâmetro, adiciona um contato (o mesmo formulário do passo 2
 * do cadastro). Com `contatoId` (só o id, nunca dado pessoal na rota), troca o telefone do contato:
 * lê o contato no `GET /entregadores/{id}` e mostra a edição só do telefone. Depois do 200 ou do
 * 201, volta ao Perfil, que recarrega a lista ao ganhar foco.
 */
export default function Contato() {
  const { contaAtiva } = useSessao();
  const { contatoId } = useLocalSearchParams<{ contatoId?: string }>();
  const [edicao, setEdicao] = useState<Edicao>({ tipo: 'carregando' });

  const carregar = useCallback(async () => {
    if (!contaAtiva || !contatoId) {
      return;
    }
    try {
      const entregador = await entregadorServico.obter(contaAtiva);
      const contato = entregador.contatos.find((item) => item.id === contatoId);
      setEdicao(contato ? { tipo: 'pronto', contato } : { tipo: 'nao-encontrado' });
    } catch (e) {
      const erro = converterErro(e);
      setEdicao({ tipo: 'erro', mensagem: erro.tipo === 'sem-conexao' ? MENSAGEM_SEM_CONEXAO : mensagemComum(erro) });
    }
  }, [contaAtiva, contatoId]);

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

  if (!contatoId) {
    return (
      <Tela>
        <Texto>Quem devemos avisar se você cair? Essa pessoa recebe o aviso do SOS.</Texto>
        <FormularioContato
          entregadorId={contaAtiva}
          aoSalvar={() => {
            AccessibilityInfo.announceForAccessibility('Contato salvo');
            router.back();
          }}
          aoPular={() => router.back()}
          textoPular="Cancelar"
        />
      </Tela>
    );
  }

  return (
    <>
      <Stack.Screen options={{ title: 'Trocar telefone do contato' }} />
      {edicao.tipo === 'carregando' ? <Carregando mensagem="Abrindo o contato..." /> : null}
      {edicao.tipo === 'erro' ? (
        <Tela>
          <EstadoErro
            mensagem={edicao.mensagem}
            aoTentarDeNovo={() => {
              setEdicao({ tipo: 'carregando' });
              void carregar();
            }}
          />
        </Tela>
      ) : null}
      {edicao.tipo === 'nao-encontrado' ? (
        <Tela>
          <EstadoVazio titulo="Este contato não existe mais." mensagem="Volte ao Perfil para ver a lista atualizada." />
        </Tela>
      ) : null}
      {edicao.tipo === 'pronto' ? <EditarContato entregadorId={contaAtiva} contato={edicao.contato} /> : null}
    </>
  );
}
