import { deleteItemAsync, getItemAsync, setItemAsync } from 'expo-secure-store';

/**
 * Contas logadas no aparelho e os tokens de cada uma, no armazenamento seguro do sistema
 * (Keystore no Android), pelo `expo-secure-store`.
 *
 * Os tokens ficam SÓ aqui: nunca no SQLite, em log, em URL, em parâmetro de rota ou na tela
 * (contrato, seção 9). Eles só saem daqui para o cabeçalho `Authorization` da instância do Axios
 * e para o corpo da renovação. Nada de nome, e-mail ou senha: só os ids e os tokens.
 *
 * Chaves (letras, números, ponto, hífen e sublinhado, como o expo-secure-store aceita):
 * - `helmcare.contas`: a lista dos entregadorId logados, na ordem em que entraram (JSON);
 * - `helmcare.contaAtiva`: o entregadorId em uso;
 * - `helmcare.<entregadorId>.acesso` e `helmcare.<entregadorId>.renovacao`: os dois tokens.
 *
 * Por que uma chave por conta e por token: o expo-secure-store recomenda valores curtos (cerca de
 * 2 KB por valor), e os tokens opacos do Identity são longos (710 caracteres cada, medidos na API
 * de desenvolvimento). Juntar as contas, ou os dois tokens, num valor só passaria do limite.
 * A lista leva só os ids: 3 GUIDs cabem com folga.
 */

/** Chave da lista de contas. */
const CHAVE_CONTAS = 'helmcare.contas';
/** Chave da conta em uso. */
const CHAVE_CONTA_ATIVA = 'helmcare.contaAtiva';

/** Chave do token de acesso de uma conta. */
function chaveAcesso(entregadorId: string): string {
  return `helmcare.${entregadorId}.acesso`;
}

/** Chave do token de renovação de uma conta. */
function chaveRenovacao(entregadorId: string): string {
  return `helmcare.${entregadorId}.renovacao`;
}

/** Os dois tokens de uma conta. */
export interface TokensConta {
  accessToken: string;
  refreshToken: string;
}

/**
 * Cópia em memória do que já foi lido, para o polling (alerta, turno e chamados, a cada 5 s)
 * não ir ao Keystore a cada requisição. Toda escrita atualiza o armazenamento e a cópia juntos.
 */
const tokensEmMemoria = new Map<string, TokensConta | null>();

/** Lê e confere a lista de contas: só textos, sem repetição. Valor corrompido vira lista vazia. */
function lerListaSalva(valor: string | null): string[] {
  if (!valor) {
    return [];
  }
  try {
    const lista: unknown = JSON.parse(valor);
    if (!Array.isArray(lista)) {
      return [];
    }
    return [...new Set(lista.filter((item): item is string => typeof item === 'string'))];
  } catch {
    return [];
  }
}

/** Ids das contas logadas no aparelho, na ordem em que entraram. */
export async function listarContas(): Promise<string[]> {
  return lerListaSalva(await getItemAsync(CHAVE_CONTAS));
}

/** Grava a lista de contas. */
async function gravarLista(lista: string[]): Promise<void> {
  await setItemAsync(CHAVE_CONTAS, JSON.stringify(lista));
}

/**
 * Salva os tokens de uma conta e a acrescenta no fim da lista (se ainda não estiver nela).
 * O limite de contas (`MAXIMO_CONTAS_APARELHO`) é conferido antes, pelo SessaoContext.
 */
export async function salvarConta(entregadorId: string, tokens: TokensConta): Promise<void> {
  await atualizarTokens(entregadorId, tokens);
  const lista = await listarContas();
  if (!lista.includes(entregadorId)) {
    await gravarLista([...lista, entregadorId]);
  }
}

/** Troca os dois tokens de uma conta (depois da renovação). */
export async function atualizarTokens(entregadorId: string, tokens: TokensConta): Promise<void> {
  await setItemAsync(chaveAcesso(entregadorId), tokens.accessToken);
  await setItemAsync(chaveRenovacao(entregadorId), tokens.refreshToken);
  tokensEmMemoria.set(entregadorId, tokens);
}

/** Tokens de uma conta, ou `null` quando ela não tem (nunca entrou ou a sessão terminou). */
export async function lerTokens(entregadorId: string): Promise<TokensConta | null> {
  const emMemoria = tokensEmMemoria.get(entregadorId);
  if (emMemoria !== undefined) {
    return emMemoria;
  }
  const accessToken = await getItemAsync(chaveAcesso(entregadorId));
  const refreshToken = await getItemAsync(chaveRenovacao(entregadorId));
  const tokens = accessToken && refreshToken ? { accessToken, refreshToken } : null;
  tokensEmMemoria.set(entregadorId, tokens);
  return tokens;
}

/**
 * Apaga só os tokens de uma conta, que continua na lista (a sessão terminou: o 401 da renovação).
 * Assim a conta aparece em "Contas neste aparelho" como "Entre de novo".
 */
export async function apagarTokens(entregadorId: string): Promise<void> {
  await deleteItemAsync(chaveAcesso(entregadorId));
  await deleteItemAsync(chaveRenovacao(entregadorId));
  tokensEmMemoria.set(entregadorId, null);
}

/** Tira a conta do aparelho ("Sair"): apaga os dois tokens e o id da lista e da conta ativa. */
export async function removerConta(entregadorId: string): Promise<void> {
  await apagarTokens(entregadorId);
  tokensEmMemoria.delete(entregadorId);
  const lista = await listarContas();
  await gravarLista(lista.filter((id) => id !== entregadorId));
  if ((await lerContaAtiva()) === entregadorId) {
    await definirContaAtiva(null);
  }
}

/** Cópia em memória da conta em uso (`undefined` até a primeira leitura). */
let contaAtivaEmMemoria: string | null | undefined;

/** Id da conta em uso, ou `null`. */
export async function lerContaAtiva(): Promise<string | null> {
  if (contaAtivaEmMemoria === undefined) {
    contaAtivaEmMemoria = await getItemAsync(CHAVE_CONTA_ATIVA);
  }
  return contaAtivaEmMemoria;
}

/** Define a conta em uso (ou `null` para nenhuma). */
export async function definirContaAtiva(entregadorId: string | null): Promise<void> {
  if (entregadorId) {
    await setItemAsync(CHAVE_CONTA_ATIVA, entregadorId);
  } else {
    await deleteItemAsync(CHAVE_CONTA_ATIVA);
  }
  contaAtivaEmMemoria = entregadorId;
}
