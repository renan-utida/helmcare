import { api } from './api';

/** Verificação de saúde da API (`GET /saude`). */
export const saudeServico = {
  /**
   * Responde texto (`Healthy`), não JSON, por isso o `responseType: 'text'`.
   * Resolve no 200; no 503 (banco fora) ou sem rede, lança `ErroApi`.
   */
  async verificar(): Promise<void> {
    await api.get<string>('/saude', { responseType: 'text' });
  },
};
