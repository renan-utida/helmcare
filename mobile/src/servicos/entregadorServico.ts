import type {
  ContatoRequisicao,
  ContatoResposta,
  CriarEntregadorRequisicao,
  EntregadorResposta,
  FichaMedicaRequisicao,
  FichaMedicaResposta,
  TelefoneRequisicao,
} from '@/tipos/contrato';

import { api } from './api';

/**
 * Rotas do entregador (seção 7). Cada função devolve o corpo tipado
 * ou lança `ErroApi` (convertido no interceptor do Axios). Desde a 1.15, todas as rotas abaixo,
 * menos o cadastro, são só do próprio entregador (403 para outro).
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

  /**
   * `GET /entregadores/{id}/ficha-medica`: a ficha completa, só para o próprio entregador, com
   * `Cache-Control: no-store`. 404 quando ainda não há ficha. Fica só na memória da tela.
   */
  async obterFichaMedica(id: string): Promise<FichaMedicaResposta> {
    const { data } = await api.get<FichaMedicaResposta>(`/entregadores/${encodeURIComponent(id)}/ficha-medica`);
    return data;
  },

  /**
   * `PUT /entregadores/{id}/contatos/{contatoId}`: o corpo inteiro do `POST` (nome, telefone e
   * parentesco). 200 com o contato (telefone mascarado); 400; 404 contato inexistente ou de outro.
   */
  async editarContato(id: string, contatoId: string, contato: ContatoRequisicao): Promise<ContatoResposta> {
    const { data } = await api.put<ContatoResposta>(
      `/entregadores/${encodeURIComponent(id)}/contatos/${encodeURIComponent(contatoId)}`,
      contato,
    );
    return data;
  },

  /** `DELETE /entregadores/{id}/contatos/{contatoId}`: 204; 404 contato inexistente ou de outro. */
  async excluirContato(id: string, contatoId: string): Promise<void> {
    await api.delete(`/entregadores/${encodeURIComponent(id)}/contatos/${encodeURIComponent(contatoId)}`);
  },

  /**
   * `PUT /entregadores/{id}/telefone`: troca o telefone (regra da seção 3). 200 com o corpo do
   * `GET /entregadores/{id}` (dados mascarados); 400 telefone inválido; 409 telefone de outro cadastro.
   */
  async trocarTelefone(id: string, dados: TelefoneRequisicao): Promise<EntregadorResposta> {
    const { data } = await api.put<EntregadorResposta>(`/entregadores/${encodeURIComponent(id)}/telefone`, dados);
    return data;
  },
};
