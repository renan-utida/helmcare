import { View } from 'react-native';

import { useTema } from '@/contextos/TemaContext';
import { leituraMascarada } from '@/componentes/LinhaInfo';
import { confereComTelefoneMascarado, somenteDigitos, validarTelefone } from '@/utils/validacao';

import { Campo } from './Campo';
import { Texto } from './Texto';

/** Os dois telefones digitados. */
export interface TelefonesDigitados {
  atual: string;
  novo: string;
}

/** Erros de cada campo (vazio quando está certo). */
export interface ErrosTrocaTelefone {
  atual?: string;
  novo?: string;
}

/**
 * Confere a troca: o "Telefone atual" precisa conferir com o mascarado da API (DDD e 4 últimos
 * números, contra engano, não como verificação de identidade), e o "Telefone novo" segue a regra
 * da seção 3 e precisa ser diferente do atual. Devolve os erros (objeto vazio quando está tudo certo).
 */
export function validarTrocaTelefone(telefones: TelefonesDigitados, mascarado: string): ErrosTrocaTelefone {
  const erros: ErrosTrocaTelefone = {};
  const erroAtual = validarTelefone(telefones.atual);
  if (erroAtual) {
    erros.atual = erroAtual;
  } else if (!confereComTelefoneMascarado(telefones.atual, mascarado)) {
    erros.atual = 'O telefone atual não confere com o do seu cadastro.';
  }
  const erroNovo = validarTelefone(telefones.novo);
  if (erroNovo) {
    erros.novo = erroNovo;
  } else if (somenteDigitos(telefones.novo) === somenteDigitos(telefones.atual)) {
    erros.novo = 'O telefone novo precisa ser diferente do atual.';
  }
  return erros;
}

/** Props do `CamposTrocaTelefone`. */
export interface CamposTrocaTelefoneProps {
  /** Telefone mascarado como a API devolve (ex.: "(11) *****-5678"), só em memória. */
  mascarado: string;
  valores: TelefonesDigitados;
  aoMudar: (valores: TelefonesDigitados) => void;
  erros: ErrosTrocaTelefone;
  /** Texto do telefone no cadastro (ex.: "Telefone no seu cadastro" ou "Telefone do contato"). */
  rotuloCadastro?: string;
}

/**
 * Par de campos da troca de telefone: mostra o telefone mascarado do cadastro, pede o "Telefone
 * atual" (conferido pelo DDD e pelos 4 últimos números) e o "Telefone novo". Usado na troca do
 * telefone do entregador e na edição do contato de emergência. A conferência fica em
 * `validarTrocaTelefone`, para as duas telas usarem a mesma regra.
 */
export function CamposTrocaTelefone({
  mascarado,
  valores,
  aoMudar,
  erros,
  rotuloCadastro = 'Telefone no seu cadastro',
}: CamposTrocaTelefoneProps) {
  const { espaco } = useTema();
  return (
    <View style={{ gap: espaco.grande }}>
      <View accessible accessibilityLabel={`${rotuloCadastro}: ${leituraMascarada(mascarado)}`}>
        <Texto variante="legenda">{rotuloCadastro}</Texto>
        <Texto variante="rotulo">{mascarado}</Texto>
      </View>
      <Campo
        rotulo="Telefone atual"
        dica="Digite o número inteiro, com DDD, para confirmar que é você"
        valor={valores.atual}
        aoMudar={(atual) => aoMudar({ ...valores, atual })}
        tipo="telefone"
        erro={erros.atual}
        maxLength={20}
      />
      <Campo
        rotulo="Telefone novo"
        dica="Com DDD, só números ou com máscara"
        valor={valores.novo}
        aoMudar={(novo) => aoMudar({ ...valores, novo })}
        tipo="telefone"
        erro={erros.novo}
        maxLength={20}
        exemplo="(11) 98765-4321"
      />
    </View>
  );
}
