import type {
  ContatoRequisicao,
  ContatoResposta,
  CriarEntregadorRequisicao,
  EntregadorResposta,
  FichaMedicaRequisicao,
  FichaMedicaResposta,
} from '@/tipos/contrato';

import { api } from './api';

/**
 * Rotas do entregador (seção 7). Cada função devolve o corpo tipado
 * ou lança `ErroApi` (convertido no interceptor do Axios).
 */
export const entregadorServico = {
  /** `POST /entregadores`: 201 com o entregador (PII mascarada); 409 CPF repetido; 400 campos. */
  async criar(dados: CriarEntregadorRequisicao): Promise<EntregadorResposta> {
    const { data } = await api.post<EntregadorResposta>('/entregadores', dados);
    return data;
  },

  /** `GET /entregadores/{id}`: 200 com os dados mascarados e só `possuiFichaMedica`; 404. */
  async obter(id: string): Promise<EntregadorResposta> {
    const { data } = await api.get<EntregadorResposta>(`/entregadores/${encodeURIComponent(id)}`);
    return data;
  },

  /** `POST /entregadores/{id}/contatos`: 201 com o contato (telefone mascarado); 400; 404. */
  async adicionarContato(id: string, contato: ContatoRequisicao): Promise<ContatoResposta> {
    const { data } = await api.post<ContatoResposta>(
      `/entregadores/${encodeURIComponent(id)}/contatos`,
      contato,
    );
    return data;
  },

  /**
   * `PUT /entregadores/{id}/ficha-medica`: substitui a ficha inteira e devolve a ficha completa.
   * A resposta fica só na memória da tela (nunca no SQLite nem em log).
   */
  async salvarFichaMedica(id: string, ficha: FichaMedicaRequisicao): Promise<FichaMedicaResposta> {
    const { data } = await api.put<FichaMedicaResposta>(
      `/entregadores/${encodeURIComponent(id)}/ficha-medica`,
      ficha,
    );
    return data;
  },
};
