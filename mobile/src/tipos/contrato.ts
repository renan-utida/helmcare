/**
 * Tipos do contrato de integração (docs/contrato-integracao.md, versão 1.14).
 * Enums da seção 4 e DTOs da seção 7, com os nomes exatos do JSON (camelCase).
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

/** Corpo do `POST /entregadores`. Os dados são sempre simulados (seção 2). */
export interface CriarEntregadorRequisicao {
  nome: string;
  cpf: string;
  cnh: string;
  telefone: string;
}

/** Contato de emergência devolvido pela API, com o telefone mascarado. */
export interface ContatoResposta {
  id: string;
  nome: string;
  telefone: string;
  parentesco: string;
}

/** Corpo do `POST /entregadores/{id}/contatos`. */
export interface ContatoRequisicao {
  nome: string;
  telefone: string;
  parentesco: string;
}

/**
 * Resposta do `POST /entregadores` e do `GET /entregadores/{id}`.
 * CPF, CNH e telefone chegam mascarados (seção 9). Da ficha médica, só `possuiFichaMedica`.
 */
export interface EntregadorResposta {
  id: string;
  nome: string;
  cpf: string;
  cnh: string;
  telefone: string;
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

/** Ficha completa devolvida só pelo `PUT`. Fica só na memória da tela, nunca no SQLite. */
export interface FichaMedicaResposta {
  tipoSanguineo: TipoSanguineo;
  alergias: string;
  condicoesCriticas: string;
  medicamentos: string;
}

// ---------------------------------------------------------------------------
// Capacete
// ---------------------------------------------------------------------------

/** Corpo do `POST /dispositivos`. O código segue o formato HC-0000. */
export interface VincularDispositivoRequisicao {
  codigo: string;
  entregadorId: string;
}

/**
 * Resposta do `POST /dispositivos`. O `qrToken` e a `urlCofre` só existem aqui,
 * e o app não guarda nenhum dos dois.
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
  latitude: number;
  longitude: number;
  localizacaoExata: boolean;
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
