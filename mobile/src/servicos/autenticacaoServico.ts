import type { LoginEntregadorResposta, LoginRequisicao, SessaoEntregadorResposta } from '@/tipos/contrato';

import { api } from './api';

/**
 * Rotas de identidade do entregador (contrato 1.15, seção 7). O login e a renovação são públicos:
 * vão sem Authorization e nunca disparam outra renovação. O corpo do login (com a senha) e a
 * resposta (com os tokens) nunca vão para log: quem chama salva os tokens só no secure-store.
 */
export const autenticacaoServico = {
  /**
   * `POST /entregadores/login`: 200 com os tokens e a sessão. 401 para e-mail ou senha errados,
   * conta bloqueada (5 erros, 15 min) e conta de socorrista, sempre com o mesmo texto; 429 acima
   * de 10 logins por minuto do mesmo IP.
   */
  async entrar(dados: LoginRequisicao): Promise<LoginEntregadorResposta> {
    const { data } = await api.post<LoginEntregadorResposta>('/entregadores/login', dados, {
      helmcare: { publica: true },
    });
    return data;
  },

  /**
   * `POST /entregadores/renovar-token`: troca o `refreshToken` por tokens novos (mesmo corpo do
   * login). 401 refresh inválido ou vencido; 429 acima do limite. O `api.ts` faz a renovação
   * automática no primeiro 401; esta função fica para quem precisar chamar direto.
   */
  async renovar(refreshToken: string): Promise<LoginEntregadorResposta> {
    const { data } = await api.post<LoginEntregadorResposta>(
      '/entregadores/renovar-token',
      { refreshToken },
      { helmcare: { publica: true } },
    );
    return data;
  },

  /**
   * `GET /entregadores/sessao`: quem é o dono do token e o que ele tem aberto (voluntário,
   * capacete, turno ativo e ocorrência ativa como vítima). Sem `entregadorId`, usa a conta ativa;
   * com ele, a de outra conta salva (o nome de cada cartão em "Contas neste aparelho").
   */
  async obterSessao(entregadorId?: string): Promise<SessaoEntregadorResposta> {
    const { data } = await api.get<SessaoEntregadorResposta>('/entregadores/sessao', {
      helmcare: entregadorId ? { entregadorId } : undefined,
    });
    return data;
  },
};
