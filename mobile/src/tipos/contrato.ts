/**
 * Tipos do contrato de integração (docs/contrato-integracao.md, versão 1.17).
 * Enums da seção 4 e DTOs da seção 7, com os nomes exatos do JSON (camelCase), conferidos
 * com os DTOs da API (api/src/HelmCare.Api/Contratos) onde o contrato só descreve em texto.
 * Datas chegam como texto ISO 8601 em UTC; ids são GUIDs em texto.
 */

// ---------------------------------------------------------------------------
// Enums (seção 4), como uniões de strings
// ---------------------------------------------------------------------------

/** Estado do capacete (ESP32), lido em `GET /turnos/{id}/alerta`. */
export type EstadoDispositivo =
  | 'DESARMADO'
  | 'ARMADO'
  | 'IMPACTO_DETECTADO'
  | 'OBSERVACAO'
  | 'CONTAGEM'
  | 'SOS_ENVIADO'
  | 'CANCELADO'
  | 'FALSO_POSITIVO';

/** Estado da ocorrência (SOS) na API. */
export type EstadoOcorrencia =
  | 'SOS_CAMADA_1'
  | 'SOS_CAMADA_2'
  | 'SOCORRO_A_CAMINHO'
  | 'ENCERRADA'
  | 'CANCELADA';

/** Desfecho de um impacto registrado. */
export type ResultadoImpacto =
  | 'IGNORADO'
  | 'FALSO_POSITIVO'
  | 'CANCELADO_PELO_ENTREGADOR'
  | 'SOS_ENVIADO';

/** Faixa do Índice de Risco do Turno: 0 a 25, 26 a 50, 51 a 75 e 76 a 100. */
export type FaixaRisco = 'BAIXO' | 'MODERADO' | 'ALTO' | 'MUITO_ALTO';

/** Tipo de voluntário da rede. */
export type TipoVoluntario = 'ENTREGADOR' | 'VIGIA_MOTO';

/** Origem de um acesso ao Cofre médico. */
export type OrigemAcessoCofre = 'SOS_ATIVO' | 'EMERGENCIA_MANUAL';

/** Motivo de uma tentativa negada no Cofre (`null` quando o acesso foi concedido). */
export type MotivoNegacaoCofre = 'SOCORRISTA_NAO_VERIFICADO' | 'ACESSO_EXPIRADO';

/** Tipo de evento MQTT do capacete (o app não usa MQTT; fica só como referência do contrato). */
export type TipoEventoMqtt =
  | 'IMPACTO_DETECTADO'
  | 'CONTAGEM_INICIADA'
  | 'CANCELADO'
  | 'FALSO_POSITIVO'
  | 'SOS';

/** Tipo de comando MQTT para o capacete (o app não usa MQTT; fica só como referência do contrato). */
export type TipoComandoMqtt = 'ARMAR' | 'DESARMAR' | 'CANCELAR' | 'SOCORRO_A_CAMINHO' | 'ENCERRAR';

/** Tipos sanguíneos aceitos pela API (seção 3), sempre em maiúsculas. */
export type TipoSanguineo = 'A+' | 'A-' | 'B+' | 'B-' | 'AB+' | 'AB-' | 'O+' | 'O-';

/** Lista dos tipos sanguíneos, na ordem em que a tela mostra os botões. */
export const TIPOS_SANGUINEOS: readonly TipoSanguineo[] = ['A+', 'A-', 'B+', 'B-', 'AB+', 'AB-', 'O+', 'O-'];

// ---------------------------------------------------------------------------
// Erros (seção 2: ProblemDetails, RFC 7807)
// ---------------------------------------------------------------------------

/**
 * Corpo de erro da API. O app decide pelo `status`, nunca pelo texto do `title` ou do `detail`.
 * No 400 de validação, as chaves de `errors` são os nomes dos campos inválidos.
 */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errors?: Record<string, string[]>;
}

// ---------------------------------------------------------------------------
// Entregador, contatos e ficha médica
// ---------------------------------------------------------------------------

/**
 * Corpo do `POST /entregadores` (desde a 1.15, com `email` e `senha`). Os dados pessoais são
 * sempre simulados (seção 2). A senha segue a seção 3 (8 a 64 caracteres, com maiúscula,
 * minúscula e número) e nunca é guardada pelo app: só vai neste corpo e no do login.
 */
export interface CriarEntregadorRequisicao {
  nome: string;
  cpf: string;
  cnh: string;
  telefone: string;
  email: string;
  senha: string;
}

// ---------------------------------------------------------------------------
// Identidade (1.15): login, renovação e sessão
// ---------------------------------------------------------------------------

/** Corpo do `POST /entregadores/login`. Nunca vai para log nem para o `converterErro`. */
export interface LoginRequisicao {
  email: string;
  senha: string;
}

/** Corpo do `POST /entregadores/renovar-token`. */
export interface RenovarTokenRequisicao {
  refreshToken: string;
}

/**
 * Resposta do `GET /entregadores/sessao`: quem é o dono do token e o que ele tem aberto.
 * Os quatro últimos campos vêm `null` quando não existem. `ocorrenciaAtivaId` é a ocorrência
 * do entregador como vítima ainda aberta (SOS_CAMADA_1, SOS_CAMADA_2 ou SOCORRO_A_CAMINHO).
 */
export interface SessaoEntregadorResposta {
  entregadorId: string;
  nome: string;
  voluntarioId: string | null;
  codigoDispositivo: string | null;
  turnoAtivoId: string | null;
  ocorrenciaAtivaId: string | null;
}

/**
 * Resposta do login e da renovação: a sessão mais os tokens. Os tokens vão direto para o
 * `expo-secure-store` e nunca para o SQLite, log, URL, parâmetro de rota ou tela (seção 9).
 */
export interface LoginEntregadorResposta extends SessaoEntregadorResposta {
  tokenType: string;
  accessToken: string;
  /** Validade do `accessToken`, em segundos (3600). */
  expiresIn: number;
  refreshToken: string;
}

/** Contato de emergência devolvido pela API, com o telefone mascarado. */
export interface ContatoResposta {
  id: string;
  nome: string;
  telefone: string;
  parentesco: string;
}

/** Corpo do `POST /entregadores/{id}/contatos` e do `PUT .../contatos/{contatoId}` (o corpo inteiro). */
export interface ContatoRequisicao {
  nome: string;
  telefone: string;
  parentesco: string;
}

/**
 * Resposta do `POST /entregadores`, do `GET /entregadores/{id}` e do `PUT .../telefone`.
 * CPF, CNH, telefone e e-mail chegam mascarados (seção 9; e-mail como `r***@exemplo.com`).
 * O `email` vem `null` só num entregador antigo sem conta. Da ficha médica, só `possuiFichaMedica`.
 */
export interface EntregadorResposta {
  id: string;
  nome: string;
  cpf: string;
  cnh: string;
  telefone: string;
  email: string | null;
  criadoEm: string;
  possuiFichaMedica: boolean;
  contatos: ContatoResposta[];
}

/** Corpo do `PUT /entregadores/{id}/ficha-medica`: substitui a ficha inteira. */
export interface FichaMedicaRequisicao {
  tipoSanguineo: TipoSanguineo;
  alergias?: string;
  condicoesCriticas?: string;
  medicamentos?: string;
}

/**
 * Ficha completa do `GET` e do `PUT /entregadores/{id}/ficha-medica` (as duas com `no-store`).
 * Campo não informado vem como texto vazio. Fica só na memória da tela, nunca no SQLite.
 */
export interface FichaMedicaResposta {
  tipoSanguineo: TipoSanguineo;
  alergias: string;
  condicoesCriticas: string;
  medicamentos: string;
}

// ---------------------------------------------------------------------------
// Capacete
// ---------------------------------------------------------------------------

/** Corpo do `PUT /entregadores/{id}/telefone`. */
export interface TelefoneRequisicao {
  telefone: string;
}

/** Corpo do `POST /dispositivos`. O código segue o formato HC-0000. */
export interface VincularDispositivoRequisicao {
  codigo: string;
  entregadorId: string;
}

/**
 * Resposta do `POST /dispositivos`. O `qrToken` e a `urlCofre` só existem aqui, e o app não
 * guarda nenhum dos dois (são credenciais, seção 9). A `urlCofre` vem relativa ("/cofre/{qrToken}").
 */
export interface DispositivoResposta {
  id: string;
  codigo: string;
  entregadorId: string;
  qrToken: string;
  urlCofre: string;
}

// ---------------------------------------------------------------------------
// Turno, risco e alerta
// ---------------------------------------------------------------------------

/** Corpo do `POST /turnos`. `inicio` e `fimPrevisto` em ISO UTC, com até 24 h de turno. */
export interface IniciarTurnoRequisicao {
  entregadorId: string;
  inicio: string;
  fimPrevisto: string;
  protecaoAtiva: boolean;
}

/** Os três componentes do Índice, cada um de 0 a 100. */
export interface ComponentesRisco {
  sinistro: number;
  chuva: number;
  calor: number;
}

/** Resposta do `POST /turnos` e do `GET /turnos/{id}/risco`. */
export interface RiscoTurnoResposta {
  turnoId: string;
  indiceRisco: number;
  faixaRisco: FaixaRisco;
  componentes: ComponentesRisco;
  explicacao: string;
}

/** Resposta do `POST /turnos/{id}/encerrar`: o turno com o risco no mesmo formato do `POST /turnos`. */
export interface TurnoResposta {
  turnoId: string;
  entregadorId: string;
  inicio: string;
  fimPrevisto: string;
  fim: string | null;
  protecaoAtiva: boolean;
  indiceRisco: number;
  faixaRisco: FaixaRisco;
  componentes: ComponentesRisco;
  explicacao: string;
}

/**
 * Resposta do `GET /turnos/{id}/alerta`. Sem capacete vinculado, todos os campos vêm `null`.
 * `contagemTerminaEm` está no relógio do capacete e serve só como informação.
 */
export interface AlertaTurnoResposta {
  estadoCapacete: EstadoDispositivo | null;
  contagemTerminaEm: string | null;
  ocorrenciaId: string | null;
  indiceCalorC: number | null;
}

// ---------------------------------------------------------------------------
// Trechos críticos
// ---------------------------------------------------------------------------

/** Um trecho da lista de trechos críticos (dado público, pode ficar no cache). */
export interface TrechoCriticoResposta {
  posicao: number;
  logradouro: string;
  latitude: number;
  longitude: number;
  quantidadeSinistrosGraves: number;
}

/** Resposta do `GET /trechos-criticos?limite=`, em ordem decrescente de sinistros graves. */
export interface TrechosCriticosResposta {
  periodo: string;
  trechos: TrechoCriticoResposta[];
}

// ---------------------------------------------------------------------------
// Rede de voluntários e ocorrências
// ---------------------------------------------------------------------------

/** Corpo do `POST /voluntarios`. O voluntário começa indisponível. */
export interface CadastrarVoluntarioRequisicao {
  entregadorId: string;
  tipo: TipoVoluntario;
}

/**
 * Corpo do `PUT /voluntarios/{id}/disponibilidade`. Só `disponivel` é obrigatório: disponível
 * exige as duas coordenadas (senão, 400); indisponível apaga a posição, então o app manda só
 * `{ disponivel: false }`.
 */
export interface DisponibilidadeRequisicao {
  disponivel: boolean;
  latitude?: number | null;
  longitude?: number | null;
}

/** Resposta do cadastro e da disponibilidade do voluntário. */
export interface VoluntarioResposta {
  voluntarioId: string;
  entregadorId: string;
  tipo: TipoVoluntario;
  disponivel: boolean;
  latitude: number | null;
  longitude: number | null;
}

/** Corpo do `POST /ocorrencias/{id}/aceites`. */
export interface AceiteRequisicao {
  voluntarioId: string;
}

/** Cronômetro da ocorrência. O app soma 1 por segundo entre as consultas, até `parado`. */
export interface CronometroResposta {
  segundosDesdeQueda: number;
  metaSegundos: number;
  parado: boolean;
}

/**
 * Resposta do `GET /ocorrencias/{id}`, do aceite, do cancelar e do encerrar.
 * A localização só é exata quando `localizacaoExata` é verdadeiro.
 */
export interface OcorrenciaResposta {
  id: string;
  estado: EstadoOcorrencia;
  camada: number;
  criadaEm: string;
  aceitaEm: string | null;
  encerradaEm: string | null;
  tempoAteAceiteSegundos: number | null;
  /** Verdadeiro quando a API encerrou a ocorrência por tempo (`TEMPO_ENCERRAMENTO_AUTOMATICO_MIN`). */
  encerradaAutomaticamente: boolean;
  latitude: number;
  longitude: number;
  localizacaoExata: boolean;
  /**
   * Só para a vítima, depois do aceite: distância (km, 1 casa) do voluntário que aceitou até a
   * queda. `null` para os outros, antes do aceite ou se o voluntário ficou indisponível.
   * A posição do voluntário nunca vem.
   */
  distanciaVoluntarioKm: number | null;
  cronometro: CronometroResposta;
}

/** Item do `GET /ocorrencias/proximas`, sempre com a localização aproximada. */
export interface OcorrenciaProximaResposta {
  ocorrenciaId: string;
  estado: EstadoOcorrencia;
  camada: number;
  criadaEm: string;
  latitude: number;
  longitude: number;
  distanciaKm: number;
  cronometro: CronometroResposta;
}

// ---------------------------------------------------------------------------
// Cofre médico: auditoria (só para o próprio entregador)
// ---------------------------------------------------------------------------

/** Socorrista de uma tentativa de acesso, como vem na auditoria. */
export interface SocorristaAuditoriaResposta {
  nome: string;
  organizacao: string;
}

/**
 * Um item do `GET /cofre/auditoria/{entregadorId}`: uma tentativa de acesso à ficha, concedida
 * ou negada. Sem dados de saúde e sem o ip. Fica só na memória da tela, nunca no SQLite.
 */
export interface ItemAuditoriaCofreResposta {
  acessadoEm: string;
  /** Fim da janela (concedido), fechamento (expirado) ou o próprio `acessadoEm` (não verificado). */
  expiraEm: string;
  origem: OrigemAcessoCofre;
  concedido: boolean;
  motivo: MotivoNegacaoCofre | null;
  socorrista: SocorristaAuditoriaResposta;
}

// ---------------------------------------------------------------------------
// Clima (1.17): cartão da aba Início, na E5
// ---------------------------------------------------------------------------

/** Resposta do `GET /clima`: condições atuais da cidade pela Open-Meteo. `503` sem a fonte. */
export interface ClimaResposta {
  temperaturaC: number;
  umidadePct: number;
  chuvaMmHora: number;
  /** Índice de calor (fórmula da seção 6.1), com 1 casa. */
  indiceCalorC: number;
  /** Verdadeiro quando o `indiceCalorC` arredondado é maior ou igual a `ALERTA_CALOR_C`. */
  alertaCalor: boolean;
  atualizadoEm: string;
  /** Crédito da licença: o cartão mostra "Dados: Open-Meteo". */
  fonte: string;
}
