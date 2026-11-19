import copy
import json

import pytest

from helmcare_cyber.pii import Categoria, Confianca, Metodo, MotorPII
from helmcare_cyber.pii.regras import (
    ETAPA_DESEMPATE,
    ORDEM_TEXTO,
    REGRAS_POR_ID,
    TODAS_AS_REGRAS,
    dobrar,
    normalizar_nome_campo,
    proteger,
)
from tests import dados_simulados as d


def categorias(achados):
    return [a.categoria for a in achados]


def unico(achados):
    assert len(achados) == 1, achados
    return achados[0]


class TestCatalogo:
    def test_ids_unicos_e_no_formato(self):
        ids = [r.id for r in TODAS_AS_REGRAS]
        assert len(ids) == len(set(ids))
        for regra_id in ids:
            prefixo, _, numero = regra_id.rpartition("-")
            assert prefixo.startswith("PII-") and numero.isdigit() and len(numero) == 2

    def test_desempate_esta_na_ordem(self):
        assert ETAPA_DESEMPATE in ORDEM_TEXTO

    def test_toda_categoria_tem_regra(self):
        assert {r.categoria for r in TODAS_AS_REGRAS} == set(Categoria)

    def test_regras_por_id(self):
        assert REGRAS_POR_ID["PII-CPF-01"].categoria is Categoria.CPF


class TestNormalizacao:
    @pytest.mark.parametrize(
        "nome", ["tipoSanguineo", "tipo_sanguineo", "TipoSanguíneo", "tipo-sanguineo", "TIPO SANGUINEO"]
    )
    def test_nome_de_campo(self, nome):
        assert normalizar_nome_campo(nome) == "tiposanguineo"

    def test_dobrar_mantem_tamanho(self):
        texto = "Hipertensão e ALÉRGICO, condições críticas"
        assert len(dobrar(texto)) == len(texto)
        assert dobrar(texto) == "hipertensao e alergico, condicoes criticas"

    def test_dobrar_caracteres_especiais(self):
        # İ minúsculo vira dois caracteres; o dobrar fica com a base para não mudar o tamanho.
        assert len(dobrar("İ́x")) == 3

    def test_proteger_mantem_tamanho(self):
        texto = f"id {d.GUID_ENTREGADOR} em 2026-11-12T21:15:30Z v3.13.17"
        protegido = proteger(texto)
        assert len(protegido) == len(texto)
        assert not any(c.isdigit() for c in protegido)


class TestPorNomeDeCampo:
    @pytest.mark.parametrize(
        "campo, valor, categoria",
        [
            ("cpf", d.CPF_VALIDO, Categoria.CPF),
            ("CPF", d.CPF_VALIDO_MASCARA, Categoria.CPF),
            ("cnh", d.CNH_VALIDA, Categoria.CNH),
            ("rg", d.RG_MASCARA, Categoria.RG),
            ("identidade", d.RG_MASCARA, Categoria.RG),
            ("telefone", d.CELULAR, Categoria.TELEFONE),
            ("celular", d.CELULAR_MASCARA, Categoria.TELEFONE),
            ("fone", d.FIXO, Categoria.TELEFONE),
            ("email", d.EMAIL, Categoria.EMAIL),
            ("e-mail", d.EMAIL, Categoria.EMAIL),
            ("E_Mail", d.EMAIL, Categoria.EMAIL),
            ("nome", "Nome Simulado", Categoria.NOME),
            ("tipoSanguineo", "O+", Categoria.SAUDE),
            ("tipo_sanguineo", "A-", Categoria.SAUDE),
            ("alergias", "dipirona", Categoria.SAUDE),
            ("condicoesCriticas", "asma", Categoria.SAUDE),
            ("condicoes_criticas", "asma", Categoria.SAUDE),
            ("medicamentos", "losartana", Categoria.SAUDE),
            ("laudo", "texto do laudo", Categoria.SAUDE),
            ("diagnóstico", "texto", Categoria.SAUDE),
            ("prontuario", "texto", Categoria.SAUDE),
            ("latitude", -23.5641, Categoria.LOCALIZACAO_EXATA),
            ("longitude", -46.6525, Categoria.LOCALIZACAO_EXATA),
            ("lat", "-23.5641", Categoria.LOCALIZACAO_EXATA),
            ("lng", -46.6525, Categoria.LOCALIZACAO_EXATA),
            ("lon", -46.6525, Categoria.LOCALIZACAO_EXATA),
            ("senha", d.SENHA, Categoria.SENHA),
            ("password", d.SENHA, Categoria.SENHA),
            ("pwd", d.SENHA, Categoria.SENHA),
            ("secret", d.SENHA, Categoria.SENHA),
            ("accessToken", d.ACCESS_TOKEN, Categoria.TOKEN_ACESSO),
            ("access_token", d.ACCESS_TOKEN, Categoria.TOKEN_ACESSO),
            ("refreshToken", d.REFRESH_TOKEN, Categoria.TOKEN_ACESSO),
            ("Authorization", "Bearer " + d.ACCESS_TOKEN, Categoria.TOKEN_ACESSO),
            ("qrToken", d.QR_TOKEN, Categoria.QR_TOKEN),
            ("qr_token", d.QR_TOKEN, Categoria.QR_TOKEN),
            ("urlCofre", "http://localhost:5000/cofre/" + d.QR_TOKEN, Categoria.QR_TOKEN),
            ("apiKey", d.CHAVE_GOOGLE, Categoria.SEGREDO_API),
        ],
    )
    def test_campo(self, motor, campo, valor, categoria):
        achado = unico(motor.varrer({campo: valor}))
        assert achado.categoria is categoria
        assert achado.metodo is Metodo.NOME_CAMPO
        assert achado.confianca is Confianca.ALTA

    def test_cpf_com_dv_errado_e_marcado(self, motor):
        # O Contrato só exige 11 dígitos: o nome do campo basta.
        achado = unico(motor.varrer({"cpf": d.CPF_DV_ERRADO}))
        assert achado.categoria is Categoria.CPF
        assert achado.dv_confere is False
        assert achado.confianca is Confianca.ALTA
        assert achado.valor_mascarado == "***.***.***-38"

    def test_cpf_e_cnh_com_dv_certo(self, motor):
        achados = motor.varrer({"cpf": d.CPF_VALIDO, "cnh": d.CNH_VALIDA})
        assert [a.dv_confere for a in achados] == [True, True]

    def test_cnh_dv_errado(self, motor):
        assert unico(motor.varrer({"cnh": d.CNH_DV_ERRADO})).dv_confere is False

    def test_dv_vazio_fora_de_cpf_e_cnh(self, motor):
        assert unico(motor.varrer({"telefone": d.CELULAR})).dv_confere is None

    def test_cpf_numerico(self, motor):
        achado = unico(motor.varrer({"cpf": int(d.CPF_VALIDO)}))
        assert achado.dv_confere is True

    def test_coordenada_aproximada(self, motor):
        achado = unico(motor.varrer({"latitude": -23.56}))
        assert achado.categoria is Categoria.LOCALIZACAO_APROXIMADA
        assert achado.ja_mascarado is True
        assert achado.regra_id == "PII-LOC-02"

    def test_coordenada_texto_invalido_e_exata(self, motor):
        achado = unico(motor.varrer({"latitude": "perto da Paulista"}))
        assert achado.categoria is Categoria.LOCALIZACAO_EXATA
        assert achado.valor_mascarado == "[REDIGIDO:LOCALIZACAO_EXATA]"

    @pytest.mark.parametrize(
        "campo, valor",
        [
            ("cpf", "***.***.***-37"),
            ("cnh", "*******8331"),
            ("telefone", "(11) *****-4321"),
            ("email", "r***@exemplo.com"),
            ("senha", "[REDIGIDO:SENHA]"),
        ],
    )
    def test_ja_mascarado(self, motor, campo, valor):
        achado = unico(motor.varrer({campo: valor}))
        assert achado.ja_mascarado is True
        assert achado.valor_mascarado == valor
        assert achado.dv_confere is None

    @pytest.mark.parametrize("valor", [None, "", "   ", True, False, []])
    def test_valor_vazio_nao_gera_achado(self, motor, valor):
        assert motor.varrer({"cpf": valor}) == []

    def test_lista_herda_categoria(self, motor):
        achados = motor.varrer({"medicamentos": ["insulina", "losartana"]})
        assert [a.caminho for a in achados] == ["$.medicamentos[0]", "$.medicamentos[1]"]
        assert set(categorias(achados)) == {Categoria.SAUDE}

    def test_dict_herda_categoria(self, motor):
        achados = motor.varrer({"authorization": {"esquema": "Bearer", "valor": d.ACCESS_TOKEN}})
        assert categorias(achados) == [Categoria.TOKEN_ACESSO, Categoria.TOKEN_ACESSO]

    def test_valor_nao_textual_em_campo_pii(self, motor):
        assert motor.varrer({"nome": object()}) == []

    def test_campo_parecido_nao_e_marcado(self, motor):
        # Comparação exata do nome normalizado: "latencia" não é "lat".
        assert motor.varrer({"latencia": 123.4567, "nomeArquivo": "relatorio.pdf", "tokenType": "Bearer"}) == []

    def test_caminho_com_chave_especial(self, motor):
        achado = unico(motor.varrer({"dados pessoais": {"cpf": d.CPF_VALIDO}}))
        assert achado.caminho == '$["dados pessoais"].cpf'


class TestTextoLivre:
    def test_cpf_com_mascara(self, motor):
        achado = unico(motor.varrer(f"Documento {d.CPF_VALIDO_MASCARA} conferido"))
        assert (achado.categoria, achado.regra_id, achado.dv_confere) == (Categoria.CPF, "PII-CPF-02", True)
        assert achado.confianca is Confianca.ALTA

    def test_cpf_meia_mascara(self, motor):
        assert categorias(motor.varrer("doc 482917365-37")) == [Categoria.CPF]

    def test_cpf_com_mascara_dv_errado_sem_contexto(self, motor):
        assert motor.varrer("protocolo 482.917.365-38") == []

    def test_cpf_com_mascara_dv_errado_com_contexto(self, motor):
        achado = unico(motor.varrer("CPF: 482.917.365-38"))
        assert achado.dv_confere is False
        assert achado.confianca is Confianca.MEDIA

    def test_rg_com_contexto(self, motor):
        achado = unico(motor.varrer(f"RG {d.RG_MASCARA} SSP"))
        assert achado.categoria is Categoria.RG

    def test_rg_sem_contexto(self, motor):
        assert motor.varrer(f"código {d.RG_MASCARA}") == []

    @pytest.mark.parametrize(
        "texto",
        [
            d.CELULAR_MASCARA,
            "+55 11 98765-4321",
            "+5511987654321",
            "55 11 98765-4321",
            "(11)98765-4321",
            "11 98765-4321",
            d.FIXO_MASCARA,
            "(21) 2345-6789",
        ],
    )
    def test_telefone_com_separador(self, motor, texto):
        achado = unico(motor.varrer(f"ligar para {texto} hoje"))
        assert achado.categoria is Categoria.TELEFONE
        assert achado.regra_id == "PII-TEL-02"

    def test_telefone_ddd_inexistente(self, motor):
        assert motor.varrer("ligar para (10) 98765-4321") == []

    def test_fixo_so_digitos_com_contexto(self, motor):
        assert unico(motor.varrer(f"telefone {d.FIXO}")).regra_id == "PII-TEL-03"

    def test_fixo_so_digitos_sem_contexto(self, motor):
        # 10 dígitos soltos podem ser um timestamp Unix: sem contexto, não marca.
        assert motor.varrer("evento 1763500000 e 1134567890") == []

    def test_telefone_com_55_so_digitos_com_contexto(self, motor):
        assert unico(motor.varrer("whatsapp 5511987654321")).categoria is Categoria.TELEFONE

    def test_email(self, motor):
        achado = unico(motor.varrer(f"contato: {d.EMAIL}."))
        assert achado.categoria is Categoria.EMAIL
        assert achado.valor_mascarado == "e***@exemplo.com"

    @pytest.mark.parametrize("tipo", ["A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-"])
    def test_tipo_sanguineo_com_contexto(self, motor, tipo):
        assert unico(motor.varrer(f"Tipo sanguíneo: {tipo}")).regra_id == "PII-SAUDE-02"

    def test_tipo_sanguineo_sem_contexto(self, motor):
        assert motor.varrer("nota A+ na prova e B- no trabalho") == []

    @pytest.mark.parametrize(
        "termo",
        [
            "alergia",
            "Alérgico",
            "ALERGICA",
            "diabetes",
            "hipertensão",
            "Hipertensao",
            "asma",
            "epilepsia",
            "cardiopatia",
            "marcapasso",
            "marca-passo",
            "anticoagulante",
            "insulina",
            "laudo",
            "prontuário",
            "diagnóstico",
            "medicamento",
            "uso contínuo",
            "condição crítica",
            "condições críticas",
        ],
    )
    def test_lexico_de_saude(self, motor, termo):
        achado = unico(motor.varrer(f"Observação: {termo} informada"))
        assert achado.categoria is Categoria.SAUDE
        assert achado.confianca is Confianca.MEDIA

    @pytest.mark.parametrize("texto", ["CID J45", "cid-10 E11.9", "CID: G40", "CID10 I10"])
    def test_cid(self, motor, texto):
        assert unico(motor.varrer(f"atestado {texto}")).regra_id == "PII-SAUDE-04"

    def test_par_de_coordenadas_exato(self, motor):
        achado = unico(motor.varrer("queda em -23.5641, -46.6525 agora."))
        assert achado.categoria is Categoria.LOCALIZACAO_EXATA
        assert achado.valor_mascarado == "-23.56, -46.65"

    def test_par_de_coordenadas_aproximado(self, motor):
        achado = unico(motor.varrer("perto de (-23.56;-46.65)"))
        assert achado.categoria is Categoria.LOCALIZACAO_APROXIMADA
        assert achado.ja_mascarado is True

    def test_par_fora_da_faixa(self, motor):
        assert motor.varrer("valores 95.1234, 200.5678") == []

    @pytest.mark.parametrize(
        "texto, categoria",
        [
            ('"latitude": -23.5641', Categoria.LOCALIZACAO_EXATA),
            ("lon=-46.6525", Categoria.LOCALIZACAO_EXATA),
            ("lat=-23.56", Categoria.LOCALIZACAO_APROXIMADA),
        ],
    )
    def test_coordenada_rotulada(self, motor, texto, categoria):
        assert unico(motor.varrer(f"log {texto}")).categoria is categoria

    @pytest.mark.parametrize(
        "texto",
        [f"senha={d.SENHA}", f"senha: {d.SENHA}", f"password={d.SENHA}", f"pwd={d.SENHA}", f'"senha": "{d.SENHA}"'],
    )
    def test_senha(self, motor, texto):
        texto_mascarado, achados = motor.mascarar(f"login falhou {texto} usuario x")
        assert unico(achados).categoria is Categoria.SENHA
        assert d.SENHA not in texto_mascarado

    def test_senha_ja_redigida(self, motor):
        assert unico(motor.varrer("senha=[REDIGIDO:SENHA]")).ja_mascarado is True

    def test_senha_vence_cpf(self, motor):
        # Credencial vem antes na ordem: o trecho é da senha.
        assert categorias(motor.varrer(f"senha={d.CPF_VALIDO}")) == [Categoria.SENHA]

    def test_bearer(self, motor):
        achado = unico(motor.varrer(f"Authorization: Bearer {d.ACCESS_TOKEN}"))
        assert achado.categoria is Categoria.TOKEN_ACESSO
        assert achado.regra_id == "PII-TOKEN-02"

    def test_jwt(self, motor):
        assert unico(motor.varrer(f"token {d.JWT} expirado")).regra_id == "PII-TOKEN-03"

    def test_bearer_sem_token(self, motor):
        assert motor.varrer('"tokenType": "Bearer"') == []

    def test_qr_token_no_caminho(self, motor):
        texto, achados = motor.mascarar(f"GET /cofre/{d.QR_TOKEN} 200")
        assert unico(achados).categoria is Categoria.QR_TOKEN
        assert texto == "GET /cofre/[REDIGIDO:QR_TOKEN] 200"

    def test_rotas_do_cofre_sem_token(self, motor):
        assert motor.varrer("GET /cofre/assets/app.js e /api/v1/cofre/auditoria/x") == []

    def test_chave_google(self, motor):
        assert unico(motor.varrer(f"GEMINI_API_KEY={d.CHAVE_GOOGLE}")).categoria is Categoria.SEGREDO_API

    def test_chave_sk(self, motor):
        achado = unico(motor.varrer(f"chave {d.CHAVE_SK}"))
        assert achado.regra_id == "PII-SEGREDO-03"
        assert achado.confianca is Confianca.MEDIA

    def test_nome_em_texto_livre_nao_e_detectado(self, motor):
        # Limitação documentada: sem NER, nome próprio em texto não é reconhecido.
        assert motor.varrer("O entregador Fulano Simulado de Tal caiu") == []


class TestDesempateOnzeDigitos:
    def test_mascara_de_cpf_decide(self, motor):
        assert unico(motor.varrer(d.CPF_VALIDO_MASCARA)).categoria is Categoria.CPF

    def test_contexto_cpf_vence_dv_de_cnh(self, motor):
        achado = unico(motor.varrer(f"CPF {d.CNH_VALIDA}"))
        assert (achado.categoria, achado.dv_confere, achado.confianca) == (Categoria.CPF, False, Confianca.MEDIA)

    def test_contexto_cnh_vence_dv_de_cpf(self, motor):
        achado = unico(motor.varrer(f"CNH {d.CPF_VALIDO}"))
        assert (achado.categoria, achado.dv_confere) == (Categoria.CNH, False)

    @pytest.mark.parametrize("palavra", ["habilitação", "carteira", "cnh nº"])
    def test_palavras_de_cnh(self, motor, palavra):
        achado = unico(motor.varrer(f"{palavra} {d.CNH_VALIDA}"))
        assert (achado.categoria, achado.dv_confere, achado.confianca) == (Categoria.CNH, True, Confianca.ALTA)

    def test_contexto_telefone_vence_dv(self, motor):
        assert unico(motor.varrer(f"telefone {d.CPF_VALIDO}")).categoria is Categoria.TELEFONE

    def test_contexto_telefone_numero_invalido(self, motor):
        achado = unico(motor.varrer("celular 12345678901"))
        assert (achado.categoria, achado.confianca) == (Categoria.TELEFONE, Confianca.MEDIA)

    def test_contexto_mais_proximo_vence(self, motor):
        achado = unico(motor.varrer(f"CPF e CNH: {d.CNH_VALIDA}"))
        assert achado.categoria is Categoria.CNH

    def test_sem_contexto_dv_cpf(self, motor):
        achado = unico(motor.varrer(f"registro {d.CPF_VALIDO}"))
        assert (achado.categoria, achado.dv_confere, achado.confianca) == (Categoria.CPF, True, Confianca.MEDIA)

    def test_sem_contexto_dv_cnh(self, motor):
        achado = unico(motor.varrer(f"registro {d.CNH_VALIDA}"))
        assert (achado.categoria, achado.regra_id) == (Categoria.CNH, "PII-CNH-03")

    def test_sem_contexto_formato_de_celular(self, motor):
        achado = unico(motor.varrer(f"registro {d.CELULAR}"))
        assert (achado.categoria, achado.regra_id) == (Categoria.TELEFONE, "PII-TEL-04")

    def test_sem_contexto_nada_confere(self, motor):
        assert motor.varrer("registro 12345678901") == []

    def test_contexto_nao_atravessa_outro_numero(self, motor):
        achados = motor.varrer(f"celular {d.CELULAR} e depois {d.CPF_VALIDO}")
        assert categorias(achados) == [Categoria.TELEFONE, Categoria.CPF]

    def test_contexto_fora_da_janela(self, motor):
        texto = "cnh " + "x" * 60 + " " + d.CPF_VALIDO
        assert unico(motor.varrer(texto)).categoria is Categoria.CPF

    @pytest.mark.parametrize(
        "texto",
        [
            f"9{d.CPF_VALIDO}",
            f"{d.CPF_VALIDO}0",
            f"pedido{d.CPF_VALIDO}",
            f"{d.CPF_VALIDO}abc",
            f"1.{d.CPF_VALIDO}",
            f"{d.CPF_VALIDO},5",
        ],
    )
    def test_numero_colado_nao_marca(self, motor, texto):
        assert motor.varrer(texto) == []


class TestFalsosPositivos:
    def test_telemetria(self, motor):
        telemetria = {
            "dispositivo": "HC-0001",
            "ts": "2026-11-12T21:15:30Z",
            "estado": "ARMADO",
            "temperaturaC": 33.4,
            "umidadePct": 58,
            "indiceCalorC": 37.1,
            "presenca": True,
            "velocidadeKmh": 32,
            "bateriaPct": 81,
            "picoG": 4.8,
        }
        assert motor.varrer(telemetria) == []

    def test_telemetria_em_texto(self, motor):
        assert motor.varrer("picoG=4.8 temperaturaC=33.4 umidadePct=58 indiceCalorC=37.1 velocidadeKmh=32") == []

    @pytest.mark.parametrize(
        "texto",
        [
            d.GUID_ENTREGADOR,
            "12345678-9012-3456-7890-123456789012",
            "2026-11-12T21:15:30Z",
            "2026-11-12T21:15:30.123+00:00",
            "2026-11-12",
            "versão 3.13.17",
            "v10.0.26200",
            "192.168.0.1",
            "HC-0001",
            "Contrato 1.17",
        ],
    )
    def test_guid_timestamp_versao(self, motor, texto):
        assert motor.varrer(f"registro {texto} ok") == []

    def test_guid_so_de_digitos_com_contexto(self, motor):
        assert motor.varrer("cpf 12345678-9012-3456-7890-482917365370") == []

    def test_numero_longo_de_telemetria(self, motor):
        assert motor.varrer({"contador": 1234567, "ts_epoch": 1763500000123}) == []


class TestEntradas:
    @pytest.mark.parametrize("dado", [None, 0, 4.8, True, [], {}, "", "texto qualquer", b"", (1, 2)])
    def test_nao_falha(self, motor, dado):
        assert motor.varrer(dado) == []

    def test_json_em_texto(self, motor):
        achados = motor.varrer(json.dumps({"cpf": d.CPF_VALIDO, "nome": "Nome Simulado"}))
        assert [a.caminho for a in achados] == ["$.cpf", "$.nome"]

    def test_json_invalido_vira_texto(self, motor):
        achado = unico(motor.varrer('{"cpf": "' + d.CPF_VALIDO + '", '))
        assert achado.metodo is Metodo.VALOR
        assert achado.caminho == "$"

    def test_json_escalar_vira_texto(self, motor):
        assert motor.varrer('"texto"') == []

    def test_bytes(self, motor):
        assert categorias(motor.varrer(f"cpf {d.CPF_VALIDO}".encode())) == [Categoria.CPF]

    def test_lista_aninhada(self, motor):
        achados = motor.varrer([{"contatos": [{"nome": "A", "telefone": d.CELULAR}]}])
        assert [a.caminho for a in achados] == ["$[0].contatos[0].nome", "$[0].contatos[0].telefone"]

    def test_tupla_mantem_tipo(self, motor):
        copia, _ = motor.mascarar(({"cpf": d.CPF_VALIDO},))
        assert isinstance(copia, tuple)

    def test_inteiro_longo_fora_de_campo_pii(self, motor):
        copia, achados = motor.mascarar({"documento": int(d.CPF_VALIDO)})
        assert categorias(achados) == [Categoria.CPF]
        assert copia == {"documento": "***.***.***-37"}

    def test_texto_em_campo_comum(self, motor):
        achados = motor.varrer({"observacao": f"cliente alérgico, cpf {d.CPF_VALIDO}"})
        assert categorias(achados) == [Categoria.SAUDE, Categoria.CPF]
        assert {a.caminho for a in achados} == {"$.observacao"}

    def test_unicode_nfkc(self, motor):
        # Dígitos de largura total viram dígitos comuns antes de casar os padrões.
        largura_total = "".join(chr(ord(c) + 0xFEE0) for c in d.CPF_VALIDO)
        achado = unico(motor.varrer(f"cpf {largura_total}"))
        assert achado.categoria is Categoria.CPF and achado.dv_confere is True

    def test_unicode_nfkc_em_campo(self, motor):
        largura_total = "".join(chr(ord(c) + 0xFEE0) for c in d.CPF_VALIDO)
        assert unico(motor.varrer({"cpf": largura_total})).dv_confere is True

    def test_chave_com_documento(self, motor):
        copia, achados = motor.mascarar({d.CPF_VALIDO: {"status": "ok"}, "contato@exemplo.com": 1})
        assert categorias(achados) == [Categoria.CPF, Categoria.EMAIL]
        assert d.CPF_VALIDO not in json.dumps(copia)
        assert all(d.CPF_VALIDO not in a.caminho for a in achados)

    def test_chave_curta_nao_e_varrida(self, motor):
        assert motor.varrer({"laudo-medico-anexo": 1, "uso": "x"}) == []


class TestMascarar:
    def test_nao_altera_entrada(self, motor):
        entrada = {"cpf": d.CPF_VALIDO, "contatos": [{"telefone": d.CELULAR}], "obs": f"email {d.EMAIL}"}
        original = copy.deepcopy(entrada)
        copia, _ = motor.mascarar(entrada)
        assert entrada == original
        assert copia is not entrada
        assert copia["contatos"] is not entrada["contatos"]

    def test_copia_mascarada(self, motor):
        copia, _ = motor.mascarar(
            {"cpf": d.CPF_VALIDO, "telefone": d.CELULAR, "email": d.EMAIL, "latitude": -23.5641, "senha": d.SENHA}
        )
        assert copia == {
            "cpf": "***.***.***-37",
            "telefone": "(11) *****-4321",
            "email": "e***@exemplo.com",
            "latitude": -23.56,
            "senha": "[REDIGIDO:SENHA]",
        }

    def test_texto_mascarado(self, motor):
        texto, _ = motor.mascarar(f"CPF {d.CPF_VALIDO_MASCARA}, tel {d.CELULAR_MASCARA}, local -23.5641, -46.6525")
        assert texto == "CPF ***.***.***-37, tel (11) *****-4321, local -23.56, -46.65"

    def test_idempotente(self, motor):
        entrada = f"CPF {d.CPF_VALIDO}, cnh {d.CNH_VALIDA}, {d.CELULAR_MASCARA}, senha={d.SENHA}, alergia"
        uma_vez, _ = motor.mascarar(entrada)
        duas_vezes, achados = motor.mascarar(uma_vez)
        assert duas_vezes == uma_vez
        assert all(a.ja_mascarado for a in achados)

    def test_idempotente_em_dict(self, motor):
        uma_vez, _ = motor.mascarar({"cpf": d.CPF_VALIDO, "latitude": -23.5641})
        duas_vezes, achados = motor.mascarar(uma_vez)
        assert duas_vezes == uma_vez
        assert all(a.ja_mascarado for a in achados)

    def test_json_volta_como_json(self, motor):
        saida, _ = motor.mascarar(json.dumps({"cpf": d.CPF_VALIDO}))
        assert json.loads(saida) == {"cpf": "***.***.***-37"}

    def test_json_sem_achado_volta_igual(self, motor):
        entrada = '{ "codigo":   "HC-0001" }'
        assert motor.mascarar(entrada) == (entrada, [])

    def test_texto_sem_achado_volta_igual(self, motor):
        assert motor.mascarar("ﬁm sem PII") == ("ﬁm sem PII", [])


def test_motor_e_reutilizavel():
    motor = MotorPII()
    assert motor.varrer({"cpf": d.CPF_VALIDO}) == motor.varrer({"cpf": d.CPF_VALIDO})
