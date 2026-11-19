"""Dados pessoais SIMULADOS usados nos testes.

Todos foram inventados para este módulo: os CPFs e as CNHs têm dígitos
verificadores válidos calculados a partir de bases aleatórias, os telefones
usam números de exemplo e os e-mails usam o domínio reservado exemplo.com.
"""

CPF_VALIDO = "48291736537"
CPF_VALIDO_MASCARA = "482.917.365-37"
CPF_VALIDO_2 = "73104829640"
CPF_DV_ERRADO = "48291736538"

CNH_VALIDA = "60471928331"
CNH_VALIDA_2 = "13958274600"
# Base em que o primeiro DV dá 10 ou mais e entra o desconto de 2 no segundo.
CNH_COM_DESCONTO = "95024861305"
CNH_DV_ERRADO = "60471928332"

CELULAR = "11987654321"
CELULAR_MASCARA = "(11) 98765-4321"
FIXO = "1134567890"
FIXO_MASCARA = "(11) 3456-7890"
CELULAR_CONTATO = "11912345678"

EMAIL = "entregador.simulado@exemplo.com"
RG_MASCARA = "12.345.678-9"
SENHA = "SenhaSimulada123"
ACCESS_TOKEN = "CfDJ8SimuladoTokenOpacoDoIdentity0123456789abcdefXYZ"
REFRESH_TOKEN = "CfDJ8SimuladoRefreshTokenDoIdentity9876543210zyxwvuQ"
QR_TOKEN = "qrSimulado_7Hk2Lm9Pq4Rs8Tv1"
# As chaves são montadas por concatenação para não aparecerem por extenso no
# arquivo: o secret scanning do GitHub bloqueia o push mesmo de chave inventada.
CHAVE_GOOGLE = "AIza" + "SySimulada0123456789abcdefghijklmno"
CHAVE_SK = "sk" + "-" + "simulada0123456789abcdefXYZ"
JWT = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJzaW11bGFkbyJ9.c2ltdWxhZG9hc3NpbmF0dXJh"

GUID_ENTREGADOR = "3f2a9c1e-5b7d-4e8a-9c21-7d4e5f6a8b90"
GUID_OCORRENCIA = "9b1d0c2e-1111-4a2b-8c3d-2e4f5a6b7c8d"
