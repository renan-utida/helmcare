import { useState } from 'react';
import { View } from 'react-native';

import { CODIGO_CAPACETE_PADRAO } from '@/config/constantes';
import { APELIDO_COM_CAPACETE, useSessao } from '@/contextos/SessaoContext';
import { useTema } from '@/contextos/TemaContext';
import { dispositivoServico } from '@/servicos/dispositivoServico';
import { converterErro } from '@/servicos/erros';
import { mensagemComum } from '@/utils/mensagensErro';
import { normalizarCodigoCapacete, validarCodigoCapacete } from '@/utils/validacao';

import { Aviso } from './Aviso';
import { Botao } from './Botao';
import { Campo } from './Campo';

/** Props do `FormularioCapacete`. */
export interface FormularioCapaceteProps {
  /** Entregador que recebe o capacete. */
  entregadorId: string;
  /** Chamado depois do 201 e de o perfil local ser atualizado. */
  aoVincular: (codigo: string) => void;
  /** Ação do botão secundário (ex.: "Não tenho capacete"). Sem ela, o botão não aparece. */
  aoPular?: () => void;
  textoPular?: string;
}

/**
 * Vincula o capacete (`POST /dispositivos`), no passo 3 do cadastro e no Perfil.
 * No 201, o perfil local guarda só o código e passa a ter o apelido "Entregador com capacete".
 * O `qrToken` e a `urlCofre` da resposta são descartados: não vão para o SQLite nem para a tela.
 */
export function FormularioCapacete({ entregadorId, aoVincular, aoPular, textoPular = 'Não tenho capacete' }: FormularioCapaceteProps) {
  const { espaco } = useTema();
  const { perfis, salvarPerfil, escolherApelido } = useSessao();
  const [codigo, setCodigo] = useState(CODIGO_CAPACETE_PADRAO);
  const [erroCodigo, setErroCodigo] = useState<string | null>(null);
  const [mensagem, setMensagem] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  async function vincular() {
    const erro = validarCodigoCapacete(codigo);
    setErroCodigo(erro);
    if (erro) {
      return;
    }
    setEnviando(true);
    setMensagem(null);
    try {
      const dispositivo = await dispositivoServico.vincular({
        codigo: normalizarCodigoCapacete(codigo),
        entregadorId,
      });
      const atual = perfis.find((perfil) => perfil.entregadorId === entregadorId);
      await salvarPerfil({
        entregadorId,
        apelido: escolherApelido(APELIDO_COM_CAPACETE, entregadorId),
        codigoCapacete: dispositivo.codigo,
        voluntarioId: atual?.voluntarioId ?? null,
        criadoEm: atual?.criadoEm ?? new Date().toISOString(),
      });
      aoVincular(dispositivo.codigo);
    } catch (e) {
      const erroApi = converterErro(e);
      if (erroApi.tipo === 'conflito') {
        setMensagem('Este capacete já está vinculado a outro cadastro.');
      } else if (erroApi.tipo === 'validacao') {
        setErroCodigo('Use o formato HC-0000, como HC-0001.');
      } else if (erroApi.tipo === 'nao-encontrado') {
        setMensagem('Cadastro não encontrado.');
      } else {
        setMensagem(mensagemComum(erroApi));
      }
    } finally {
      setEnviando(false);
    }
  }

  return (
    <View style={{ gap: espaco.grande }}>
      <Campo
        rotulo="Código do capacete"
        dica="Está na etiqueta do capacete, no formato HC-0000"
        valor={codigo}
        aoMudar={setCodigo}
        tipo="codigo"
        autoCapitalize="characters"
        erro={erroCodigo}
        maxLength={7}
      />
      {mensagem ? <Aviso tipo="erro" mensagem={mensagem} /> : null}
      <Botao titulo="Vincular capacete" icone="link" aoPressionar={vincular} carregando={enviando} />
      {aoPular ? (
        <Botao
          titulo={textoPular}
          variante="secundaria"
          aoPressionar={aoPular}
          desabilitado={enviando}
          accessibilityHint="Segue sem capacete. Dá para vincular depois, no Perfil."
        />
      ) : null}
    </View>
  );
}
