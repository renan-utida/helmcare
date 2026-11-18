/**
 * Passagem de dados entre duas telas SÓ em memória, para o que não pode ir na URL da rota nem no
 * histórico de navegação (parâmetros de rota ficam guardados pelo Expo Router e aparecem em log
 * de navegação). Cada valor é lido uma vez e apagado na leitura, e tudo some ao fechar o app.
 *
 * Usos: o e-mail que preenche o login depois de um cadastro cujo login automático falhou; os
 * dados devolvidos pela troca de telefone, para o Perfil não consultar de novo; e o QR Code do
 * capacete logo depois do `POST /dispositivos` (o `urlCofre` leva o `qrToken`, que é credencial:
 * ele nunca vai para a rota, para o SQLite nem para log, e some quando a tela do QR fecha).
 */

import type { EntregadorResposta } from '@/tipos/contrato';

/** O que cada chave leva. */
interface ValoresDoFluxo {
  /** E-mail digitado no cadastro, para preencher o login (dado pessoal: nunca na rota). */
  emailLogin: string;
  /** Corpo do 200 do `PUT /entregadores/{id}/telefone` (dados mascarados). */
  entregadorAtualizado: EntregadorResposta;
  /** Código do capacete e o endereço completo da página do Cofre, para a tela do QR. */
  qrCapacete: { codigo: string; urlCofreCompleta: string };
}

/** Chave de um valor do fluxo. */
type ChaveFluxo = keyof ValoresDoFluxo;

/** Valores guardados agora, por chave. */
const valores = new Map<ChaveFluxo, ValoresDoFluxo[ChaveFluxo]>();

/** Guarda um valor para a próxima tela ler. */
export function guardarParaProximaTela<C extends ChaveFluxo>(chave: C, valor: ValoresDoFluxo[C]): void {
  valores.set(chave, valor);
}

/** Lê e apaga o valor da chave, ou `null` quando não há. */
export function retirarDoFluxo<C extends ChaveFluxo>(chave: C): ValoresDoFluxo[C] | null {
  const valor = valores.get(chave);
  valores.delete(chave);
  // O Map guarda os valores de todas as chaves juntos; quem gravou nesta chave foi o
  // `guardarParaProximaTela` com o mesmo tipo, então o valor é desta chave.
  return (valor as ValoresDoFluxo[C] | undefined) ?? null;
}
