# HelmCare: proteção de dados (Cybersecurity)

O HelmCare guarda dados de quem está na rua trabalhando: documentos, telefone, ficha médica, localização no momento de uma queda e as credenciais da conta. Este módulo existe para que esses dados do entregador só apareçam onde o [Contrato de Integração](../docs/contrato-integracao.md) permite (seção 9) e nunca em logs, mensagens MQTT, respostas para terceiros ou chamadas a serviços externos.

## O que o módulo faz

1. **Mapeamento de PII.** Um motor de parsing percorre payloads JSON, objetos aninhados e texto livre e aponta cada dado pessoal encontrado: CPF, CNH, RG, telefone, e-mail, nome, saúde, localização exata ou aproximada, senhas, tokens de acesso, o `qrToken` do capacete e chaves de API. CPF e CNH têm os dígitos verificadores conferidos, não só o formato. Cada achado traz o caminho do dado, a categoria, a regra que disparou, a confiança e a versão mascarada, nunca o valor original.
2. **Taint Analysis.** O dado pessoal é marcado na fonte (cadastro, ficha médica, ocorrência, login, dispositivo) e acompanhado até cada saída: log, mensagem de erro, push, MQTT, chamado ao voluntário, Cofre, persistência, documentação. Um guarda decide pela política da seção 9 do Contrato o que pode sair, para quem e em que forma; no modo PROTECAO ele bloqueia, redige, mascara ou arredonda antes de o dado sair, e no modo AUDITORIA só aponta. Cada decisão vira um evento de segurança com um rastro explicável, sem o valor original.
3. **Tradução por IA e painel Risco Cidadão.** Cada evento de segurança é traduzido por um LLM numa frase calma, para uma pessoa leiga, que diz o que poderia ter acontecido com os dados e o que o sistema fez. Ao LLM vão só a categoria, o destino, a ação, o estado e a cor, nunca um valor. A saída do LLM é validada, e uma reserva por tabela responde quando não há chave ou o LLM falha. O indicador Risco Cidadão resume os eventos num índice de 0 a 100 com semáforo, rótulo e ícone.

## Estrutura

```
cyber/
  helmcare_cyber/
    config.py          configuração lida de variáveis de ambiente
    pii/
      categorias.py    enums Categoria, Confianca e Metodo
      validadores.py   CPF, CNH, telefone, tipo sanguíneo e coordenadas
      mascaras.py      máscaras do Contrato, redação e reconhecimento de valor já mascarado
      regras.py        catálogo de regras (dados), nomes de campo e desempate dos 11 dígitos
      motor.py         MotorPII, Achado e ValorProtegido
      avaliacao.py     métricas do motor sobre um corpus rotulado
    taint/
      modelo.py        Fonte, Destino, Destinatario, Acao, Modo, Estado e ContextoSaida
      politica.py      tabela da política (seção 9) e os ids de regra TA-...
      registro.py      TaintRegistry: valores das fontes, só em memória
      rastro.py        Rastro e Passo (fonte, propagação, sanitizador, destino)
      sanitizadores.py mascarar, redigir, bloquear, arredondar, hash_senha e projetar_campos
      guarda.py        GuardaTaint: detecta, decide, aplica e gera eventos
      filtro_log.py    filtro do logging e saída protegida para print
    eventos/
      modelo.py        EventoSeguranca
      repositorio.py   RepositorioEventos (memória e JSONL opcional)
    simulacao/
      dados_simulados.py   payloads simulados no formato do Contrato
      cenarios.py          cenários C1 a C11
    risco/
      risco_cidadao.py     índice de 0 a 100, semáforo, pesos e fatores
    ia/
      prompt.py            consulta, descrições em linguagem simples e prompt do sistema
      reserva.py           frases de reserva por tabela, motivos e origem da mensagem
      cliente_llm.py       cliente do Gemini (httpx), chave só no cabeçalho
      tradutor.py          validação da saída, cache, disjuntor e tradução de eventos e do risco
  scripts/avaliar_motor.py   tabela de precisão e recall por categoria
  scripts/demo.py            cenários C1 a C11 nos dois modos
  scripts/demo_risco.py      Risco Cidadão de cada situação, com a mensagem
  scripts/testar_ia.py       três traduções de exemplo e a repetição pelo cache
  tests/                     testes pytest (pii, taint, eventos, simulacao, risco, ia)
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
python -m pytest -q --cov=helmcare_cyber         # com cobertura
python scripts/avaliar_motor.py                  # métricas sobre o corpus
python scripts/demo.py                           # cenários do Taint Analysis
python scripts/demo_risco.py                     # Risco Cidadão, só com a reserva (sem rede)
python scripts/testar_ia.py --sem-dotenv         # tradução sem carregar o cyber/.env
```

As variáveis de ambiente esperadas estão em `.env.example`. Para usar a tradução por IA, copie o arquivo para `cyber/.env` e preencha a chave e o modelo; o `.env` é ignorado pelo Git. A biblioteca só lê `os.environ`: quem carrega o `cyber/.env` são os scripts (`testar_ia.py` sem `--sem-dotenv` e `demo_risco.py --com-ia`), pela função `carregar_dotenv()`, que nunca sobrescreve uma variável que já existe no ambiente e nunca imprime o conteúdo. A chave não entra na `Config`.

| Variável | Padrão | Uso |
|---|---|---|
| `GEMINI_API_KEY` | vazio | Chave da tradução dos alertas. Sem ela, as mensagens saem da reserva por tabela |
| `GEMINI_MODEL` | vazio | Modelo do Gemini (sem valor padrão no código). Sem ele, também vale a reserva |
| `CYBER_MODO` | `PROTECAO` | Modo do guarda criado por `criar_guarda()`. Valor desconhecido vira `PROTECAO` |
| `CYBER_EVENTOS_JSONL` | vazio | Arquivo em que cada evento de segurança vira uma linha JSON. Vazio deixa os eventos só em memória |
| `RISCO_JANELA_MIN` | `1440` | Janela, em minutos, do indicador Risco Cidadão |

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
- `transformar(dado, substituir)` faz o mesmo percurso, mas quem decide o novo valor de cada achado é a função `substituir(achado, valor_atual)` (devolver `None` mantém o valor). É por ela que o guarda do Taint Analysis aplica a política no lugar exato de cada dado.
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

## Limitações do motor de PII

- **Nome de pessoa em texto livre não é detectado pelo motor.** Reconhecer "Fulano de Tal" no meio de uma frase exige reconhecimento de entidades (NER), que o motor não usa. O nome só é marcado pelo campo `nome`. O corpus mantém dois casos desse tipo como limitação conhecida, que aparecem como falso negativo de NOME na avaliação. No Taint Analysis, o registro de taint cobre o caso mais comum: o nome que entrou por uma fonte é reconhecido pelo valor em qualquer saída.
- Nomes de campo são comparados por igualdade depois da normalização: `telefoneContato` ou `cpfEntregador` não são reconhecidos pelo nome (o valor ainda passa pelas regras de texto).
- Telefone escrito com ponto entre todos os grupos (`11.98765.4321`) tem o formato de um número de versão e não é marcado.
- O léxico de saúde marca o termo com confiança MEDIA mesmo em uso técnico ("diagnóstico de rede").
- No cálculo do segundo DV da CNH, quando o resto menos o desconto fica negativo, o motor soma 11 (implementação de referência em Python). Há implementações publicadas que tratam esse caso como CNH inválida; fora dele, todas concordam.

## Taint Analysis

O Taint Analysis segue o dado pessoal da fonte ao destino e decide, em cada saída, se ele pode passar. É uma verificação em tempo de execução: o guarda olha o que realmente está saindo, depois de todas as concatenações e f-strings do código.

### Modelo

| Elemento | O que é | Exemplos |
|---|---|---|
| Fonte | Onde o dado pessoal entra | `CADASTRO_ENTREGADOR`, `FICHA_MEDICA`, `CONTATO_EMERGENCIA`, `OCORRENCIA_SOS`, `TELEMETRIA_MQTT`, `LOGIN`, `DISPOSITIVO` |
| Propagação | Como o dado anda pelo código | f-string em mensagem de log, objeto anexado a outro payload, valor repetido numa mensagem de erro |
| Sanitizador | Função que torna o dado seguro | `mascarar`, `redigir`, `bloquear`, `arredondar_localizacao`, `hash_senha` (PBKDF2-HMAC-SHA256, 600 mil iterações), `projetar_campos` (lista branca de campos) |
| Destino | Por onde o dado sai, e para quem | `LOG`, `MENSAGEM_ERRO`, `NOTIFICACAO_PUSH`, `MQTT_PAYLOAD`, `CHAMADO_VOLUNTARIO`, `COFRE_SOCORRISTA`, `PERSISTENCIA`, `DOCUMENTACAO_PUBLICA` e outros, com o `Destinatario` (próprio entregador, voluntário antes ou depois do aceite, contato de emergência, socorrista verificado na janela ou não autorizado, anônimo, outro usuário, sistema) |

A detecção numa saída usa duas fontes de evidência:

- o **motor de PII**, que reconhece formatos (CPF, telefone, e-mail, coordenadas) e nomes de campo;
- o **registro de taint** (`TaintRegistry`), que guarda em memória os valores que entraram por uma fonte e os reconhece em qualquer saída, mesmo no meio de uma f-string. É ele que acha o nome do entregador numa linha de log. Documentos e telefones são reconhecidos em qualquer formatação (`48291736537` e `482.917.365-37` são o mesmo CPF). O registro nunca é gravado nem serializado.

### Os dois modos

| Modo | O que faz | Estado do dado que a política manda tratar |
|---|---|---|
| `PROTECAO` | Aplica a ação numa cópia do payload antes de ele sair | `NEUTRALIZADO` |
| `AUDITORIA` | Deixa a saída passar intacta e só registra | `EXPOSTO` |

Um dado que a política deixa passar fica `PERMITIDO` nos dois modos, assim como um valor que já estava na forma exigida (por exemplo, o CPF já mascarado na resposta ao próprio entregador). A decisão geral de uma saída é o pior estado entre os seus dados: `EXPOSTO`, depois `NEUTRALIZADO`, depois `PERMITIDO`.

As ações são `PERMITIR`, `MASCARAR` (máscaras do Contrato), `ARREDONDAR` (2 casas, com 0,5 para cima), `REDIGIR` (`[REDIGIDO:CPF]`) e `BLOQUEAR` (`[BLOQUEADO:CPF]`).

### Política

A política fica em `taint/politica.py` como uma tabela de dados, com negação por padrão: toda combinação que não está na tabela é bloqueada. A ordem de avaliação é: valor já redigido, destinatário negado, linha do destino com o destinatário, linha do destino para qualquer destinatário e, por fim, a negação por padrão.

| Regra | Destino | Destinatário | O que pode sair |
|---|---|---|---|
| TA-MASCARADO-01 | qualquer | qualquer | Valor já redigido (`[REDIGIDO:...]` ou `[BLOQUEADO:...]`), porque não sobra nada do dado |
| TA-NEGADO-01 | qualquer | `SOCORRISTA_NAO_AUTORIZADO`, `ANONIMO`, `OUTRO_USUARIO` | Nada. Vence todas as regras abaixo |
| TA-PROPRIO-01 | `RESPOSTA_PROPRIO_ENTREGADOR` | `PROPRIO_ENTREGADOR` | Saúde, telefone, nome e localização; CPF, CNH, RG e e-mail mascarados; nenhuma credencial |
| TA-PROPRIO-02 | `RESPOSTA_PROPRIO_ENTREGADOR` | `PROPRIO_ENTREGADOR`, com `credencial_autorizada` | Token de acesso e `qrToken`, só no corpo do login, da renovação e do `POST /dispositivos` |
| TA-VOLUNT-01 | `CHAMADO_VOLUNTARIO`, `NOTIFICACAO_PUSH` | `VOLUNTARIO_ANTES_ACEITE` | Só a localização: a aproximada como está e a exata arredondada para 2 casas |
| TA-ACEITOU-01 | `CHAMADO_VOLUNTARIO`, `NOTIFICACAO_PUSH` | `VOLUNTARIO_QUE_ACEITOU` | Só a localização, inclusive a exata |
| TA-CONTATO-01 | `CONTATO_EMERGENCIA`, ou `NOTIFICACAO_PUSH` | qualquer, ou `CONTATO_EMERGENCIA` | Só a localização, exata ou aproximada |
| TA-COFRE-01 | `COFRE_SOCORRISTA` | `SOCORRISTA_VERIFICADO_NA_JANELA` | Os campos `tipoSanguineo`, `alergias` e `condicoesCriticas` |
| TA-COFRE-02 | `COFRE_SOCORRISTA` | `SOCORRISTA_VERIFICADO_NA_JANELA` | Nome e telefone dentro de `$.contatos[n]` |
| TA-COFRE-03 | `COFRE_SOCORRISTA` | `SOCORRISTA_VERIFICADO_NA_JANELA` | Nada além dos dois itens acima (medicamentos, nome, CPF, CNH, localização e credenciais do entregador são bloqueados) |
| TA-LOG-01 | `LOG` | qualquer | Nada: todo dado pessoal é redigido, inclusive a localização aproximada |
| TA-MQTT-01 | `MQTT_PAYLOAD` | qualquer | A coordenada, se o payload não tiver nenhuma identificação pessoal (só o código do dispositivo) |
| TA-MQTT-02 | `MQTT_PAYLOAD` | qualquer | Nada, se houver identificação pessoal: o dado e a coordenada são bloqueados |
| TA-PUBLICO-01 | `RESPOSTA_API_PUBLICA`, `MENSAGEM_ERRO`, `AUDITORIA`, `DOCUMENTACAO_PUBLICA` | qualquer | Nada |
| TA-PERS-01 | `PERSISTENCIA` | qualquer | Tudo, menos senha em texto puro, token de acesso e segredo de API |
| TA-PERS-02 | `PERSISTENCIA` | qualquer | Senha no formato de hash (`pbkdf2_sha256$...` ou o hash do ASP.NET Core Identity, que começa em `AQAAAA`) |
| TA-PADRAO-01 | combinação fora da tabela | | Nada (negação por padrão) |

Duas regras são do próprio guarda: TA-REG-01 identifica um achado feito pelo registro de taint, e TA-ERRO-01 marca uma saída bloqueada inteira por erro interno.

**Canais autorizados.** São os usos legítimos que a política deixa passar e que também geram evento (`PERMITIDO`), como prova do uso correto: a localização exata para o voluntário que aceitou e para o contato de emergência, a ficha mínima e os contatos no Cofre para o socorrista verificado na janela, a coordenada no MQTT sem identificação pessoal, os tokens no corpo do login e o `qrToken` no `POST /dispositivos`, os próprios dados para o entregador e a senha como hash na persistência.

### Guarda, rastro e eventos

```python
from helmcare_cyber.taint import ContextoSaida, Destinatario, Destino, Fonte, GuardaTaint, Modo

guarda = GuardaTaint(modo=Modo.PROTECAO)
guarda.registrar_fonte(Fonte.CADASTRO_ENTREGADOR, cadastro)
resultado = guarda.verificar_saida(push, ContextoSaida(Destino.NOTIFICACAO_PUSH, Destinatario.VOLUNTARIO_ANTES_ACEITE))
resultado.payload_seguro    # o que pode sair
resultado.decisao_geral     # PERMITIDO, NEUTRALIZADO ou EXPOSTO
print(resultado.rastro.para_texto())
```

`verificar_saida` nunca altera o payload recebido e nunca levanta exceção por causa do conteúdo. Se algo falhar por dentro, a saída sai bloqueada inteira (`[BLOQUEADO:ERRO_INTERNO]`) e o erro vai para o log só com o tipo da exceção, sem o conteúdo.

O **rastro** lista os passos do dado, só com valores mascarados:

```
FONTE        CADASTRO_ENTREGADOR POST /entregadores campo $.cpf [CPF] = ***.***.***-37
PROPAGACAO   f-string em mensagem de log: logger.info(f"cadastro {dados}")
DESTINO      LOG para SISTEMA, REGRA TA-LOG-01, ACAO REDIGIR, NEUTRALIZADO [CPF, CNH, TELEFONE, EMAIL, NOME, SENHA]
```

Cada saída gera um **evento de segurança** por categoria e estado, com `id`, `ts` (UTC), `cenario`, `fonte`, `destino`, `destinatario`, `categoria`, `acao`, `estado`, `regraId`, `modo`, `quantidade` e `caminhos` (os campos agrupados) e o `rastro`. Os campos `mensagemCidadao` e `origemMensagem` ficam vazios para a tradução dos alertas. O evento não tem o valor original nem peso de risco: o peso é calculado pelo indicador Risco Cidadão. Os eventos ficam em memória no `RepositorioEventos` e, com `CYBER_EVENTOS_JSONL`, também num arquivo, um por linha.

### Proteção do log

```python
from helmcare_cyber.taint.filtro_log import instalar_filtro, proteger_print

instalar_filtro(logging.getLogger("helmcare.api"), guarda)
logger.info(f"cadastro {dados}")      # sai com [REDIGIDO:CPF], [REDIGIDO:NOME]...

with proteger_print(guarda):
    print(f"senha={senha}")           # sai senha=[REDIGIDO:SENHA]
```

O filtro formata a mensagem (`msg % args`), passa pelo guarda com destino `LOG` e troca a mensagem pela versão segura. Um dict passado direto ao logger vai como estrutura, para os nomes de campo ajudarem. Em AUDITORIA, o log sai intacto e o evento fica `EXPOSTO`. Se o filtro falhar, a linha vira `[LOG BLOQUEADO: erro no filtro]`. O filtro não registra nada pelo próprio logging, para não entrar em recursão.

### Cenários

Cada cenário reproduz um erro de código num caminho de saída, com dados simulados, e passa pelas funções reais do guarda.

| Id | Cenário | Rótulo | Destino e destinatário | PROTECAO | AUDITORIA |
|---|---|---|---|---|---|
| C1 | Cadastro em log por f-string | Simulado | `LOG`, `SISTEMA` | CPF, CNH, telefone, e-mail, senha e nome redigidos | `EXPOSTO` |
| C2 | Senha impressa no log de login | Simulado | `LOG`, `SISTEMA` | E-mail e senha redigidos | `EXPOSTO` |
| C3 | Erro de validação repete CPF e telefone | Simulado | `MENSAGEM_ERRO`, `PROPRIO_ENTREGADOR` | Bloqueados no `detail` e no `errors` | `EXPOSTO` |
| C4 | Ficha médica no chamado ao voluntário | Simulado | `CHAMADO_VOLUNTARIO`, `VOLUNTARIO_ANTES_ACEITE` | Saúde bloqueada; localização aproximada permitida | `EXPOSTO` |
| C5 | Identificação pessoal no payload MQTT | Simulado | `MQTT_PAYLOAD`, `SISTEMA` | Nome, telefone, CPF e coordenada bloqueados | `EXPOSTO` |
| C6 | Push com localização exata antes do aceite | Simulado | `NOTIFICACAO_PUSH`, `VOLUNTARIO_ANTES_ACEITE` | Coordenada arredondada para 2 casas | `EXPOSTO` |
| C7 | Localização exata nos canais autorizados | Simulado | `CHAMADO_VOLUNTARIO`, `VOLUNTARIO_QUE_ACEITOU` e `CONTATO_EMERGENCIA` | `PERMITIDO` | `PERMITIDO` |
| C8 | Cofre com medicamentos por engano | Simulado | `COFRE_SOCORRISTA`, `SOCORRISTA_VERIFICADO_NA_JANELA` | Ficha mínima e contatos permitidos; medicamentos bloqueado | `EXPOSTO` |
| C9 | Cofre para socorrista não autorizado | Simulado | `COFRE_SOCORRISTA`, `SOCORRISTA_NAO_AUTORIZADO` | Tudo bloqueado | `EXPOSTO` |
| C10 | Senha em texto puro na persistência | Simulado | `PERSISTENCIA`, `SISTEMA` | Senha pura bloqueada; com `hash_senha`, permitida | `EXPOSTO` |
| C11 | `qrToken` em documentação pública | Real (reproduzido com token simulado) | `DOCUMENTACAO_PUBLICA`, `ANONIMO` | `qrToken` bloqueado na legenda | `EXPOSTO` |

O C11 reproduz um achado real de auditoria da documentação do projeto (o endereço `/cofre/{qrToken}` exposto num texto público), com um token inventado no lugar do original.

### Demonstração

```bash
python scripts/demo.py                                  # C1 a C11 em AUDITORIA e depois em PROTECAO
python scripts/demo.py --cenario C1                     # só o C1, nos dois modos
python scripts/demo.py --cenario C6 --modo protecao     # um cenário num modo
```

Para cada cenário, o script mostra o id, o título, o rótulo, a fonte, o destino, a saída emitida, a decisão geral, as regras aplicadas e o rastro. O repositório de eventos é zerado entre os modos. A saída emitida em AUDITORIA mostra os valores simulados sem proteção, de propósito: é o "antes" de cada cenário.

### Limitações do Taint Analysis

- O guarda vê só as saídas que passam por ele (`verificar_saida`, o filtro de log e `proteger_print`). Um caminho de saída que não chama o guarda não é verificado: o rastreamento é em tempo de execução, não uma análise estática do código.
- O registro de taint guarda só valores com 4 caracteres ou mais. O tipo sanguíneo (`O+`) é reconhecido pelo nome do campo ou pela palavra de contexto, não pelo registro.
- O registro reconhece o valor como ele entrou (nomes sem diferenciar maiúsculas; documentos e telefones com qualquer separador). Um nome abreviado ou escrito de outro jeito não é reconhecido.
- O registro vale para o processo em que os valores foram registrados: ele fica em memória e não é compartilhado.
- Um valor de saúde comum registrado na ficha (como "Asma") passa a ser apontado em qualquer saída em que a palavra apareça. É um falso positivo que pende para o lado seguro.

## Tradução por IA e Risco Cidadão

Cada evento de segurança vira uma frase curta para uma pessoa leiga de 60 anos, e o conjunto dos eventos vira o indicador Risco Cidadão, um número de 0 a 100 com semáforo.

### O que vai ao LLM e o que nunca vai

O LLM recebe só a classificação do evento, em cinco campos (`ConsultaTraducao`): categoria, destino, ação, estado e cor do semáforo. Cada campo vira um texto fixo em linguagem simples, e a mensagem enviada tem quatro linhas:

```
Em risco: o código do QR do seu capacete
Onde poderia aparecer: num documento público
O que o sistema fez: não conseguiu impedir, só registrou o ocorrido
Cor do semáforo: amarelo, atenção
```

Nunca vão ao LLM: o valor do dado (nem mascarado), o payload, o rastro, o caminho do campo, o id do evento ou qualquer dado do entregador. A chave vai só no cabeçalho `x-goog-api-key`, nunca na URL nem no corpo. Nenhuma mensagem de erro do cliente mostra a chave ou a URL; de uma resposta de erro, só entra a mensagem do Google (`error.message`), numa linha, com até 150 caracteres e com a chave trocada por `***`. Os testes enviam os eventos dos 11 cenários nos dois modos e conferem que nenhum valor simulado aparece no que sai para o LLM.

### Validação da saída

A resposta do LLM é tratada como não confiável. `validar_mensagem` aceita só JSON com a chave `mensagem` (uma cerca ` ```json ` é tolerada) e recusa a mensagem vazia, com mais de 280 caracteres, com mais de 2 frases, com travessão, com termo técnico (PII, log, logs, payload, MQTT, API, token, hash, endpoint, JSON, taint, como palavra inteira) ou em que o motor de PII encontre qualquer dado pessoal. Uma resposta recusada não entra no cache.

### Reserva, cache e disjuntor

Quando não há chave ou modelo, quando o LLM falha ou quando a resposta é recusada, a mensagem sai da **reserva por tabela**: uma frase montada com os mesmos textos da consulta, sempre igual para a mesma consulta. O painel mostra o selo **"modelo de reserva"** junto dessas frases. A tradução informa a origem (`IA` ou `RESERVA`) e, na reserva, o motivo: `SEM_CHAVE`, `TIMEOUT`, `ERRO_HTTP`, `ERRO_REDE`, `RESPOSTA_INVALIDA` ou `DISJUNTOR`. Numa falha do LLM, o motivo vem com o diagnóstico, por exemplo `ERRO_HTTP (503: The model is overloaded.)`, para saber se foi sobrecarga do serviço, limite de uso ou modelo errado.

| Estado | Frase de reserva |
|---|---|
| `EXPOSTO` | "{Tema} apareceu {onde}, e o sistema não conseguiu impedir, só registrou o ocorrido. A equipe precisa corrigir isso o quanto antes." |
| `NEUTRALIZADO` | "{Tema} poderia ter aparecido {onde}, mas o sistema {ação} antes que saísse. Seus dados continuam protegidos, e a equipe só precisa corrigir o código." |
| `PERMITIDO` | "O sistema usou {tema} {onde}, porque esse é um caminho autorizado. Não há nada a fazer." |

Os verbos concordam em número com o tema ("seus dados de saúde apareceram"). Toda combinação de categoria, destino, estado e ação passa na mesma validação das respostas do LLM. Sem nenhum evento que pese, a mensagem é "Tudo certo: seus dados estão protegidos e nada importante aconteceu."

O **cache** guarda as respostas válidas do LLM por consulta: a mesma combinação de categoria, destino, ação, estado e cor não chama o LLM de novo. Falhas passageiras do serviço (HTTP 429, 500, 502, 503 e 504) ganham **uma nova tentativa** depois de 1 s; erros do pedido (400, 401, 403 e 404), tempo esgotado e falha de conexão não repetem. São no máximo 2 tentativas de até 8 s cada, então uma consulta nunca passa de 17 s. O **disjuntor** abre por 60 s depois de um tempo esgotado, de uma falha de rede ou de um erro HTTP que continuou depois da nova tentativa; enquanto ele está aberto, a tradução vai direto para a reserva, para o painel não esperar o tempo limite a cada evento. Uma resposta recusada não abre o disjuntor. Cache e disjuntor são seguros para uso entre threads.

### Risco Cidadão

```
pontos do evento = 10 x peso da categoria x fator do destino x fator do estado
volume = min(25, 2 x (N - 1)), N = pares (destino, fonte) distintos entre os eventos que pesam
índice = min(100, pior evento + volume), arredondado com 0,5 para cima
```

O pior evento puxa o índice, porque um único vazamento de saúde já é perigo; o volume soma pouco e tem teto, para mostrar que o problema se espalhou sem esconder a gravidade do pior. Só entram os eventos da janela `RISCO_JANELA_MIN` (padrão 1440 minutos). As contas usam `Decimal`, então 10 x 9 x 0,9 x 0,3 dá exatamente 24,3.

| Categoria | Peso |
|---|---|
| `SENHA` | 10 |
| `SAUDE` | 9 |
| `TOKEN_ACESSO`, `SEGREDO_API` | 8 |
| `LOCALIZACAO_EXATA`, `CPF`, `CNH`, `RG` | 7 |
| `TELEFONE`, `EMAIL`, `NOME` | 5 |
| `QR_TOKEN` | 4 (sozinho, o QR não abre a ficha: o Cofre exige socorrista verificado e janela aberta) |
| `LOCALIZACAO_APROXIMADA` | 2 |
| Sem categoria (erro interno do guarda, TA-ERRO-01) | 5 |

| Destino | Fator |
|---|---|
| `RESPOSTA_API_PUBLICA`, `MENSAGEM_ERRO`, `MQTT_PAYLOAD`, `DOCUMENTACAO_PUBLICA` | 1,0 |
| `CHAMADO_VOLUNTARIO`, `NOTIFICACAO_PUSH`, `COFRE_SOCORRISTA`, `CONTATO_EMERGENCIA`, `RESPOSTA_PROPRIO_ENTREGADOR` | 0,9 |
| `AUDITORIA` | 0,6 |
| `LOG` | 0,5 |
| `PERSISTENCIA` | 0,4 |

| Estado | Fator |
|---|---|
| `EXPOSTO` | 1,0 |
| `NEUTRALIZADO` | 0,3 |
| `PERMITIDO` | 0 (uso legítimo de canal autorizado: não pesa e não conta no volume) |

| Faixa | Cor | Rótulo | Ícone |
|---|---|---|---|
| 0 a 29 | `VERDE` | Seus dados estão protegidos | `OK` |
| 30 a 59 | `AMARELO` | Atenção: houve tentativa de vazamento | `ATENCAO` |
| 60 a 100 | `VERMELHO` | Perigo: dado sensível exposto | `PERIGO` |

O painel mostra o rótulo e o ícone junto da cor, para quem não distingue cores. O resultado (`RiscoCidadao.para_dict()`) traz também o pior evento, o volume, a janela, os contadores (eventos, neutralizados, expostos e permitidos) e o "por quê": até 3 eventos que mais pesaram, do maior para o menor e, no empate, do mais recente, só com categoria, destino, estado e pontos. A mensagem do painel traduz o evento que mais pesou, com a cor do índice geral.

**Calibração** (conferida por teste, com os eventos reais dos cenários):

| Situação | Pior evento | Volume | Índice | Cor |
|---|---|---|---|---|
| Sem eventos | 0 | 0 | 0 | `VERDE` |
| Senha redigida em log (NEUTRALIZADO), como no C1 | 15 | 0 | 15 | `VERDE` |
| Saúde exposta em resposta pública | 90 | 0 | 90 | `VERMELHO` |
| Senha exposta em log | 50 | 0 | 50 | `AMARELO` |
| `qrToken` exposto em documentação pública (C11 em AUDITORIA) | 40 | 0 | 40 | `AMARELO` |
| C1 a C10 em PROTECAO (8 pares) | 24,3 | 14 | 38 | `AMARELO` |
| C1 a C10 em AUDITORIA | 81 | 14 | 95 | `VERMELHO` |
| C1 a C11 em AUDITORIA (9 pares) | 81 | 16 | 97 | `VERMELHO` |

### Como rodar

```bash
python scripts/demo_risco.py               # cada situação com índice, cor, por quê e mensagem de reserva (sem rede)
python scripts/demo_risco.py --com-ia      # o mesmo, carregando cyber/.env e usando o Gemini

python scripts/testar_ia.py --sem-dotenv   # sem carregar cyber/.env: mostra a reserva
python scripts/testar_ia.py                # carrega cyber/.env: usa o Gemini, se houver chave e modelo
```

`testar_ia.py` diz se a chave está configurada (sim ou não, nunca a chave), mostra o modelo, traduz uma consulta de cada cor e repete a primeira para mostrar o cache. Os dois scripts mostram o motivo da reserva com o diagnóstico, como `motivo da reserva: ERRO_HTTP (503: ...)`. Os dois scripts saem com código 0 também quando caem na reserva.

## Regras de dados pessoais

Seção 9 do [contrato de integração](../docs/contrato-integracao.md).
