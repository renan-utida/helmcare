BEGIN;

CREATE TABLE alembic_version (
    version_num VARCHAR(32) NOT NULL, 
    CONSTRAINT alembic_version_pkc PRIMARY KEY (version_num)
);

-- Running upgrade  -> 0001

CREATE TABLE conjunto_trechos_criticos (
    id UUID NOT NULL, 
    periodo VARCHAR(100) NOT NULL, 
    fonte TEXT NOT NULL, 
    criterio TEXT NOT NULL, 
    hash_arquivo VARCHAR(64) NOT NULL, 
    carregado_em TIMESTAMP WITH TIME ZONE NOT NULL, 
    CONSTRAINT pk_conjunto_trechos_criticos PRIMARY KEY (id), 
    CONSTRAINT uq_conjunto_trechos_criticos_hash_arquivo UNIQUE (hash_arquivo)
);

CREATE TABLE trecho_critico (
    id UUID NOT NULL, 
    conjunto_id UUID NOT NULL, 
    posicao INTEGER NOT NULL, 
    logradouro VARCHAR(200) NOT NULL, 
    latitude FLOAT NOT NULL, 
    longitude FLOAT NOT NULL, 
    quantidade_sinistros_graves INTEGER NOT NULL, 
    CONSTRAINT pk_trecho_critico PRIMARY KEY (id), 
    CONSTRAINT fk_trecho_critico_conjunto_id_conjunto_trechos_criticos FOREIGN KEY(conjunto_id) REFERENCES conjunto_trechos_criticos (id), 
    CONSTRAINT uq_trecho_critico_conjunto_id_posicao UNIQUE (conjunto_id, posicao), 
    CONSTRAINT ck_trecho_critico_posicao_minima CHECK (posicao >= 1), 
    CONSTRAINT ck_trecho_critico_quantidade_nao_negativa CHECK (quantidade_sinistros_graves >= 0)
);

INSERT INTO alembic_version (version_num) VALUES ('0001') RETURNING alembic_version.version_num;

-- Running upgrade 0001 -> 0002

CREATE TABLE calculo_indice (
    id UUID NOT NULL, 
    calculado_em TIMESTAMP WITH TIME ZONE NOT NULL, 
    inicio TIMESTAMP WITH TIME ZONE NOT NULL, 
    fim TIMESTAMP WITH TIME ZONE NOT NULL, 
    chuva_mm_hora FLOAT NOT NULL, 
    temperatura_c FLOAT NOT NULL, 
    umidade_pct FLOAT NOT NULL, 
    indice_calor_c FLOAT NOT NULL, 
    componente_sinistro INTEGER NOT NULL, 
    componente_chuva INTEGER NOT NULL, 
    componente_calor INTEGER NOT NULL, 
    indice_risco INTEGER NOT NULL, 
    faixa_risco VARCHAR(20) NOT NULL, 
    explicacao TEXT NOT NULL, 
    versao_modelo VARCHAR(20) NOT NULL, 
    regra_agregacao VARCHAR(50) NOT NULL, 
    CONSTRAINT pk_calculo_indice PRIMARY KEY (id), 
    CONSTRAINT ck_calculo_indice_componente_sinistro_0_a_100 CHECK (componente_sinistro BETWEEN 0 AND 100), 
    CONSTRAINT ck_calculo_indice_componente_chuva_0_a_100 CHECK (componente_chuva BETWEEN 0 AND 100), 
    CONSTRAINT ck_calculo_indice_componente_calor_0_a_100 CHECK (componente_calor BETWEEN 0 AND 100), 
    CONSTRAINT ck_calculo_indice_indice_risco_0_a_100 CHECK (indice_risco BETWEEN 0 AND 100), 
    CONSTRAINT ck_calculo_indice_faixa_risco_valida CHECK (faixa_risco IN ('BAIXO', 'MODERADO', 'ALTO', 'MUITO_ALTO'))
);

CREATE TABLE calculo_indice_hora (
    id UUID NOT NULL, 
    calculo_id UUID NOT NULL, 
    hora_inicio_utc TIMESTAMP WITH TIME ZONE NOT NULL, 
    hora_local INTEGER NOT NULL, 
    dia_semana INTEGER NOT NULL, 
    mes INTEGER NOT NULL, 
    feriado BOOLEAN NOT NULL, 
    previsao_sinistros FLOAT NOT NULL, 
    componente_sinistro_hora FLOAT NOT NULL, 
    minutos_no_turno FLOAT NOT NULL, 
    CONSTRAINT pk_calculo_indice_hora PRIMARY KEY (id), 
    CONSTRAINT fk_calculo_indice_hora_calculo_id_calculo_indice FOREIGN KEY(calculo_id) REFERENCES calculo_indice (id) ON DELETE CASCADE, 
    CONSTRAINT uq_calculo_indice_hora_calculo_id_hora_inicio_utc UNIQUE (calculo_id, hora_inicio_utc), 
    CONSTRAINT ck_calculo_indice_hora_hora_local_0_a_23 CHECK (hora_local BETWEEN 0 AND 23)
);

UPDATE alembic_version SET version_num='0002' WHERE alembic_version.version_num = '0001';

COMMIT;

