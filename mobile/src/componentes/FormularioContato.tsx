import { useState } from 'react';
import { View } from 'react-native';

import { useTema } from '@/contextos/TemaContext';
import { entregadorServico } from '@/servicos/entregadorServico';
import { converterErro } from '@/servicos/erros';
import type { ContatoResposta } from '@/tipos/contrato';
import { gerarContatoSimulado, PARENTESCOS, type Parentesco } from '@/utils/dadosSimulados';
import { errosDosCampos, MENSAGEM_CONFIRA_CAMPOS, mensagemComum } from '@/utils/mensagensErro';
import { validarNome, validarParentesco, validarTelefone } from '@/utils/validacao';

import { Aviso } from './Aviso';
import { Botao } from './Botao';
import { Campo } from './Campo';
import { GrupoOpcoes } from './GrupoOpcoes';

/** Campos do formulário, com os nomes do JSON do contrato. */
const CAMPOS = ['nome', 'telefone', 'parentesco'] as const;
type CampoContato = (typeof CAMPOS)[number];

/** Props do `FormularioContato`. */
export interface FormularioContatoProps {
  /** Entregador dono do contato. */
  entregadorId: string;
  /** Chamado depois do 201, com o contato devolvido (telefone mascarado). */
  aoSalvar: (contato: ContatoResposta) => void;
  /** Ação do botão secundário (ex.: "Adicionar depois"). Sem ela, o botão não aparece. */
  aoPular?: () => void;
  textoPular?: string;
}

/**
 * Formulário do contato de emergência, usado no passo 2 do cadastro e no modal "Adicionar contato".
 * Envia o `POST /entregadores/{id}/contatos`. Os dados ficam só na API, nunca no SQLite.
 */
export function FormularioContato({ entregadorId, aoSalvar, aoPular, textoPular = 'Adicionar depois' }: FormularioContatoProps) {
  const { espaco } = useTema();
  const [nome, setNome] = useState('');
  const [telefone, setTelefone] = useState('');
  const [parentesco, setParentesco] = useState<Parentesco | null>(null);
  const [erros, setErros] = useState<Partial<Record<CampoContato, string>>>({});
  const [mensagem, setMensagem] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  /** Preenche com um contato fictício (o contrato só aceita dados simulados). */
  function preencherSimulado() {
    const simulado = gerarContatoSimulado();
    setNome(simulado.nome);
    setTelefone(simulado.telefone);
    setParentesco(simulado.parentesco);
    setErros({});
    setMensagem(null);
  }

  async function salvar() {
    const novosErros: Partial<Record<CampoContato, string>> = {};
    const erroNome = validarNome(nome);
    const erroTelefone = validarTelefone(telefone);
    const erroParentesco = validarParentesco(parentesco ?? '');
    if (erroNome) novosErros.nome = erroNome;
    if (erroTelefone) novosErros.telefone = erroTelefone;
    if (erroParentesco) novosErros.parentesco = erroParentesco;
    setErros(novosErros);
    if (Object.keys(novosErros).length > 0) {
      setMensagem(MENSAGEM_CONFIRA_CAMPOS);
      return;
    }

    setEnviando(true);
    setMensagem(null);
    try {
      const contato = await entregadorServico.adicionarContato(entregadorId, {
        nome: nome.trim(),
        telefone,
        parentesco: parentesco ?? '',
      });
      aoSalvar(contato);
    } catch (e) {
      const erro = converterErro(e);
      if (erro.tipo === 'validacao') {
        setErros(errosDosCampos(erro.campos, CAMPOS));
        setMensagem(MENSAGEM_CONFIRA_CAMPOS);
      } else if (erro.tipo === 'nao-encontrado') {
        setMensagem('Cadastro não encontrado na API. Confira o perfil em uso nos Ajustes.');
      } else {
        setMensagem(mensagemComum(erro));
      }
    } finally {
      setEnviando(false);
    }
  }

  return (
    <View style={{ gap: espaco.grande }}>
      <Botao
        titulo="Preencher com dados simulados"
        variante="secundaria"
        icone="shuffle"
        aoPressionar={preencherSimulado}
        accessibilityHint="Preenche o contato com dados fictícios, como pede o projeto"
      />
      <Campo rotulo="Nome do contato" valor={nome} aoMudar={setNome} erro={erros.nome} maxLength={120} />
      <Campo
        rotulo="Telefone do contato"
        dica="Com DDD, só números ou com máscara"
        valor={telefone}
        aoMudar={setTelefone}
        tipo="telefone"
        erro={erros.telefone}
        maxLength={20}
      />
      <GrupoOpcoes
        rotulo="Parentesco"
        opcoes={PARENTESCOS}
        selecionada={parentesco}
        aoSelecionar={setParentesco}
        erro={erros.parentesco}
      />
      {mensagem ? <Aviso tipo="erro" mensagem={mensagem} /> : null}
      <Botao titulo="Salvar contato" icone="person-add" aoPressionar={salvar} carregando={enviando} />
      {aoPular ? <Botao titulo={textoPular} variante="secundaria" aoPressionar={aoPular} desabilitado={enviando} /> : null}
    </View>
  );
}
