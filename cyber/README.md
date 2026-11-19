# HelmCare: proteção de dados (Cybersecurity)

O HelmCare guarda dados de quem está na rua trabalhando: documentos, telefone, ficha médica, localização no momento de uma queda e as credenciais da conta. Este módulo existe para que esses dados do entregador só apareçam onde o [Contrato de Integração](../docs/contrato-integracao.md) permite (seção 9) e nunca em logs, mensagens MQTT, respostas para terceiros ou chamadas a serviços externos.

## O que o módulo faz

1. **Mapeamento de PII.** Um motor de parsing percorre payloads JSON, objetos aninhados e texto livre e aponta cada dado pessoal encontrado: CPF, CNH, RG, telefone, e-mail, nome, saúde, localização exata ou aproximada, senhas, tokens de acesso, o `qrToken` do capacete e chaves de API. CPF e CNH têm os dígitos verificadores conferidos, não só o formato. Cada achado traz o caminho do dado, a categoria, a regra que disparou, a confiança e a versão mascarada, nunca o valor original.
2. **Taint Analysis.** Os dados marcados pelo motor são tratados como contaminados e acompanhados até as saídas do sistema (log, MQTT, resposta HTTP, armazenamento local do app). Uma saída insegura que recebe um dado contaminado é bloqueada e registrada como evidência.
3. **Tradução por IA e painel Risco Cidadão.** Cada falha encontrada é traduzida por um LLM em um alerta claro, que diz o que vazou, para onde e como corrigir, sem enviar ao LLM nenhum valor pessoal (só categoria, caminho e regra). O painel Risco Cidadão resume esses alertas num indicador de exposição dos dados do entregador.

## Estrutura

```
cyber/
  helmcare_cyber/pii/
    categorias.py    enums Categoria, Confianca e Metodo
    validadores.py   CPF, CNH, telefone, tipo sanguíneo e coordenadas
    mascaras.py      máscaras do Contrato, redação e reconhecimento de valor já mascarado
    regras.py        catálogo de regras (dados), nomes de campo e desempate dos 11 dígitos
    motor.py         MotorPII, Achado e ValorProtegido
    avaliacao.py     métricas do motor sobre um corpus rotulado
  scripts/avaliar_motor.py   tabela de precisão e recall por categoria
  tests/pii/                 testes pytest
  tests/corpus/corpus_pii.json   corpus rotulado (positivos e negativos)
```

## Como rodar (Git Bash)

Requer Python 3.13.

```bash
cd cyber
py -3.13 -m venv .venv
source .venv/Scripts/activate
python -m pip install -r requirements.txt

python -m pytest -q                              # todos os testes
python -m pytest -q --cov=helmcare_cyber.pii     # com cobertura
python scripts/avaliar_motor.py                  # métricas sobre o corpus
```

As variáveis de ambiente esperadas estão em `.env.example`. A chave do LLM só é lida do ambiente e nunca fica em arquivo versionado.

## Uso do motor

```python
from helmcare_cyber.pii import MotorPII

motor = MotorPII()
achados = motor.varrer({"cpf": "482.917.365-37", "telefone": "11987654321"})
copia, achados = motor.mascarar("Queda em -23.5641, -46.6525, contato (11) 98765-4321")
# copia == "Queda em -23.56, -46.65, contato (11) *****-4321"
```

- `varrer(dado)` aceita dict, list (aninhados), str com JSON e texto livre, e devolve uma lista de `Achado`.
- `mascarar(dado)` devolve a cópia profunda mascarada e os achados, sem alterar a entrada. Para texto, a saída sai normalizada em NFKC; para JSON em texto, sai JSON.
- `Achado` tem `caminho`, `categoria`, `metodo` (`NOME_CAMPO` ou `VALOR`), `confianca` (`ALTA` ou `MEDIA`), `regra_id`, `dv_confere` (`True`, `False` ou `None`), `ja_mascarado` e `valor_mascarado`. `para_dict()` e `para_json()` só levam esses campos.

## Categorias e regras

Os identificadores seguem o formato `PII-CATEGORIA-NN`. As regras `-01` (e `PII-LOC-02`) valem por **nome de campo**; as demais valem pelo **valor**, em texto livre.

| Regra | Categoria | O que detecta | Exige | Confiança |
|---|---|---|---|---|
| PII-CPF-01 | CPF | campo `cpf` | nome do campo (marca mesmo com DV errado) | ALTA |
| PII-CPF-02 | CPF | `482.917.365-37` ou `482917365-37` | DV válido ou contexto `cpf` | ALTA com DV, MEDIA só com contexto |
| PII-CPF-03 | CPF | 11 dígitos soltos | desempate | ALTA com contexto e DV, MEDIA nos outros casos |
| PII-CNH-01 | CNH | campo `cnh` | nome do campo | ALTA |
| PII-CNH-02 | CNH | 11 dígitos com `cnh`, `habilitação` ou `carteira` antes | desempate | ALTA com DV, MEDIA sem |
| PII-CNH-03 | CNH | 11 dígitos sem contexto | DV da CNH | MEDIA |
| PII-RG-01 | RG | campos `rg` e `identidade` | nome do campo | ALTA |
| PII-RG-02 | RG | `12.345.678-9` | contexto `rg` ou `identidade` | ALTA |
| PII-TEL-01 | TELEFONE | campos `telefone`, `celular` e `fone` | nome do campo | ALTA |
| PII-TEL-02 | TELEFONE | `(11) 98765-4321`, `+55 11 3456-7890` | DDD oficial e formato de celular ou fixo | ALTA |
| PII-TEL-03 | TELEFONE | 10, 12 ou 13 dígitos, e 11 dígitos com contexto | contexto `telefone`, `celular`, `fone`, `whatsapp` ou `contato` | ALTA |
| PII-TEL-04 | TELEFONE | 11 dígitos sem contexto e sem DV de CPF ou CNH | formato de celular válido | MEDIA |
| PII-EMAIL-01 | EMAIL | campos `email` e `e-mail` | nome do campo | ALTA |
| PII-EMAIL-02 | EMAIL | endereço de e-mail | nada | ALTA |
| PII-NOME-01 | NOME | campo `nome` | nome do campo | ALTA |
| PII-SAUDE-01 | SAUDE | `tipoSanguineo`, `alergias`, `condicoesCriticas`, `medicamentos`, `laudo`, `diagnostico`, `prontuario` | nome do campo | ALTA |
| PII-SAUDE-02 | SAUDE | `A+`, `AB-`, `O+` e os demais tipos | contexto `sangue` ou `tipo sanguíneo` | ALTA |
| PII-SAUDE-03 | SAUDE | alergia, alérgico, diabetes, hipertensão, asma, epilepsia, cardiopatia, marcapasso, anticoagulante, insulina, laudo, prontuário, diagnóstico, medicamento, uso contínuo, condição crítica | nada (sem diferenciar acentos e maiúsculas) | MEDIA |
| PII-SAUDE-04 | SAUDE | `CID J45`, `CID-10 E11.9` | nada | ALTA |
| PII-LOC-01 | LOCALIZACAO_EXATA | `latitude`, `longitude`, `lat`, `lng`, `lon` com 3 ou mais casas | nome do campo | ALTA |
| PII-LOC-02 | LOCALIZACAO_APROXIMADA | os mesmos campos com até 2 casas | nome do campo | ALTA |
| PII-LOC-03 | LOCALIZACAO_EXATA | par `-23.5641, -46.6525` | faixa válida e 3 ou mais casas | ALTA |
| PII-LOC-04 | LOCALIZACAO_APROXIMADA | par `-23.56, -46.65` | faixa válida e até 2 casas | ALTA |
| PII-LOC-05 | LOCALIZACAO_EXATA | `lat=-23.5641`, `"longitude": -46.6525` | 3 ou mais casas | ALTA |
| PII-LOC-06 | LOCALIZACAO_APROXIMADA | `lat=-23.56` | até 2 casas | ALTA |
| PII-SENHA-01 | SENHA | campos `senha`, `password`, `pwd` e `secret` | nome do campo | ALTA |
| PII-SENHA-02 | SENHA | `senha=...`, `senha: ...`, `password=...`, `pwd=...` | nada | ALTA |
| PII-TOKEN-01 | TOKEN_ACESSO | `accessToken`, `refreshToken`, `authorization` | nome do campo | ALTA |
| PII-TOKEN-02 | TOKEN_ACESSO | `Bearer` seguido de token | nada | ALTA |
| PII-TOKEN-03 | TOKEN_ACESSO | JWT (`eyJ...`) | nada | ALTA |
| PII-QR-01 | QR_TOKEN | campos `qrToken` e `urlCofre` | nome do campo | ALTA |
| PII-QR-02 | QR_TOKEN | caminho `/cofre/{token}` com 16 ou mais caracteres | nada | ALTA |
| PII-SEGREDO-01 | SEGREDO_API | campos `apiKey`, `chaveApi` e `geminiApiKey` | nome do campo | ALTA |
| PII-SEGREDO-02 | SEGREDO_API | chave do Google (`AIza` e mais 35 caracteres) | nada | ALTA |
| PII-SEGREDO-03 | SEGREDO_API | chave genérica `sk-...` | nada | MEDIA |

**Nomes de campo** são comparados sem acento, sem maiúsculas e sem separadores: `tipoSanguineo`, `tipo_sanguineo` e `Tipo Sanguíneo` são o mesmo campo. Quando o campo é uma lista ou um objeto (`"medicamentos": ["...", "..."]`), cada valor dentro dele herda a categoria. Um campo `null`, vazio ou booleano não gera achado.

**Ordem de prioridade em texto.** Quando dois padrões casam no mesmo trecho, fica a regra que vem antes: segredos e tokens, `qrToken`, senha, e-mail, CPF com máscara, RG, coordenadas, desempate dos 11 dígitos, telefone, CID, tipo sanguíneo e léxico de saúde. Assim, `senha=48291736537` é uma senha, não um CPF.

### Desempate dos 11 dígitos

Onze dígitos podem ser CPF, CNH ou celular. O motor decide nesta ordem:

1. Com pontos e traço (`482.917.365-37`), é CPF.
2. Uma palavra de contexto até 40 caracteres antes do número decide (`cpf`; `cnh`, `habilitação`, `carteira`; `telefone`, `celular`, `fone`, `whatsapp`, `contato`). Se houver mais de uma, vale a mais próxima. O contexto não atravessa outro número: em `celular 11987654321 e 48291736537`, a palavra "celular" descreve só o primeiro.
3. Sem contexto, vale o dígito verificador: primeiro o do CPF, depois o da CNH.
4. Se nenhum DV confere e o formato é de celular, é telefone.
5. Número colado a letras ou a uma sequência maior de dígitos não é marcado.

Em texto livre, CPF e CNH só são marcados com DV válido ou com contexto. Por nome de campo, a marcação é sempre feita (o Contrato só exige 11 dígitos) e `dv_confere` registra se a conta bate.

## Máscaras (seção 9 do Contrato)

| Categoria | Máscara |
|---|---|
| CPF | `***.***.***-12` |
| CNH | `*******8900` |
| Celular | `(11) *****-1234` |
| Telefone fixo | `(11) ****-4567` |
| E-mail | `r***@exemplo.com` |
| Localização exata | arredondada para 2 casas decimais (`-23.56`), com 0,5 para cima |
| Localização aproximada | já é a forma permitida, fica como está |
| RG, nome, saúde, senha, tokens, `qrToken`, segredos | `[REDIGIDO:CATEGORIA]`, sem nenhum caractere do valor |

`redigir(categoria)` produz o marcador `[REDIGIDO:CPF]` (e os demais), sem dígito algum, para uso em logs e eventos. As máscaras são idempotentes, e um valor que já está numa dessas formas é reconhecido: o achado sai com `ja_mascarado` verdadeiro e não conta como vazamento (por exemplo, o `email` mascarado do `GET /entregadores/{id}`).

## O que nunca é marcado

- Números de telemetria do capacete (`picoG`, `temperaturaC`, `umidadePct`, `indiceCalorC`, `velocidadeKmh`, `bateriaPct`) e o código do dispositivo (`HC-0001`). Valores numéricos só são avaliados pelo nome do campo; a exceção são inteiros com 10 dígitos ou mais, que podem ser um documento gravado como número.
- GUIDs, timestamps ISO 8601 e números de versão (`3.13.17`): antes dos padrões numéricos, esses trechos são neutralizados, então nenhum dígito dentro deles vira CPF ou telefone.
- 10 dígitos soltos sem contexto (podem ser um timestamp Unix).

## Como o valor original é protegido

O `Achado` guarda o valor original dentro de um `ValorProtegido`, só para uso interno em memória (`achado.valor_original()`). Esse embrulho não aparece no `repr` nem no `str`, faz o `json.dumps` falhar em vez de serializar e bloqueia `pickle`. `para_dict()` e `para_json()` levam só os campos públicos. Os testes em `tests/pii/test_nao_vaza_valor.py` conferem que o valor não aparece em nenhuma dessas saídas.

## Avaliação

O corpus `tests/corpus/corpus_pii.json` tem casos positivos e negativos para todas as categorias, além de negativos de telemetria, GUID, timestamp e versão. `scripts/avaliar_motor.py` imprime verdadeiros positivos, falsos positivos, falsos negativos, precisão e recall por categoria, e falha se algum negativo de telemetria, GUID, timestamp ou versão for marcado. Todos os dados do corpus e dos testes são simulados: os CPFs e as CNHs foram inventados com dígitos verificadores válidos.

## Limitações

- **Nome de pessoa em texto livre não é detectado.** Reconhecer "Fulano de Tal" no meio de uma frase exige reconhecimento de entidades (NER), que o motor não usa. O nome só é marcado pelo campo `nome`. O corpus mantém dois casos desse tipo como limitação conhecida, que aparecem como falso negativo de NOME na avaliação.
- Nomes de campo são comparados por igualdade depois da normalização: `telefoneContato` ou `cpfEntregador` não são reconhecidos pelo nome (o valor ainda passa pelas regras de texto).
- Telefone escrito com ponto entre todos os grupos (`11.98765.4321`) tem o formato de um número de versão e não é marcado.
- O léxico de saúde marca o termo com confiança MEDIA mesmo em uso técnico ("diagnóstico de rede").
- No cálculo do segundo DV da CNH, quando o resto menos o desconto fica negativo, o motor soma 11 (implementação de referência em Python). Há implementações publicadas que tratam esse caso como CNH inválida; fora dele, todas concordam.

## Regras de dados pessoais

Seção 9 do [contrato de integração](../docs/contrato-integracao.md).
