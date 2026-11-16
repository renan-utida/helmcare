import { deleteItemAsync, getItemAsync, setItemAsync } from 'expo-secure-store';

/**
 * Guarda do token de acesso do login da API (preparação: o login chega com o contrato v1.15).
 * O token fica SÓ aqui, no armazenamento seguro do sistema (Keystore no Android). Nunca vai
 * para o SQLite, para log, para parâmetro de rota, para URL nem para a tela.
 */

/** Chave do token no armazenamento seguro (letras, números, ponto, hífen e sublinhado). */
const CHAVE_TOKEN = 'helmcare.token_acesso';

/** Salva o token de acesso, trocando o anterior. */
export async function salvarToken(token: string): Promise<void> {
  await setItemAsync(CHAVE_TOKEN, token);
}

/** Lê o token de acesso, ou `null` quando não há login. */
export async function lerToken(): Promise<string | null> {
  return getItemAsync(CHAVE_TOKEN);
}

/** Apaga o token de acesso (sair da conta ou sessão expirada). */
export async function apagarToken(): Promise<void> {
  await deleteItemAsync(CHAVE_TOKEN);
}
