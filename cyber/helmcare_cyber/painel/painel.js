// Painel Risco Cidadão. Sem biblioteca externa e sem polling contínuo: o painel
// atualiza depois de cada ação e no botão Atualizar. Todo texto que vem do
// servidor entra no DOM com textContent (nunca como HTML), contra XSS.
"use strict";

(function () {
  const API = "/cyber/v1";
  const TEMPO_LIMITE_MS = 20000;
  // A tradução pode esperar o LLM (até cerca de 17 s no pior caso).
  const TEMPO_LIMITE_TRADUCAO_MS = 30000;

  const ICONES = { OK: "✔", ATENCAO: "⚠", PERIGO: "✖" };
  const NOME_ICONE = { OK: "OK", ATENCAO: "Atenção", PERIGO: "Perigo" };
  const NOME_MODO = { PROTECAO: "PROTEÇÃO", AUDITORIA: "AUDITORIA" };
  const EXPLICACAO_MODO = {
    PROTECAO: "O sistema impede o vazamento antes de o dado sair.",
    AUDITORIA: "O sistema só registra o vazamento, sem impedir. Serve para demonstração.",
  };
  const CORES = ["cor-VERDE", "cor-AMARELO", "cor-VERMELHO", "cor-carregando", "cor-erro"];
  const MENSAGEM_SEM_SERVIDOR =
    "Não foi possível falar com o servidor. Confira se ele está ligado e clique em Atualizar.";

  let modoAtual = null;
  let ocupado = false;
  let pedidoTraducao = 0;

  const el = (id) => document.getElementById(id);

  function criar(tag, texto, classe) {
    const elemento = document.createElement(tag);
    if (texto !== undefined && texto !== null) {
      elemento.textContent = String(texto);
    }
    if (classe) {
      elemento.className = classe;
    }
    return elemento;
  }

  async function chamar(caminho, opcoes, tempoLimiteMs) {
    const controle = new AbortController();
    const relogio = setTimeout(() => controle.abort(), tempoLimiteMs || TEMPO_LIMITE_MS);
    try {
      const resposta = await fetch(API + caminho, {
        method: "GET",
        ...(opcoes || {}),
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        signal: controle.signal,
      });
      if (!resposta.ok) {
        throw new Error("o servidor respondeu " + resposta.status);
      }
      return await resposta.json();
    } finally {
      clearTimeout(relogio);
    }
  }

  function mostrarStatus(texto, erro) {
    const status = el("status");
    status.textContent = texto;
    status.classList.toggle("erro", Boolean(erro));
  }

  function bloquear(sim) {
    ocupado = sim;
    document.querySelectorAll("button.acao").forEach((botao) => {
      botao.disabled = sim;
    });
    el("principal").setAttribute("aria-busy", sim ? "true" : "false");
  }

  // ------------------------------------------------------------------
  // Desenho das partes do painel
  // ------------------------------------------------------------------

  function desenharModo(modo) {
    modoAtual = modo;
    const atual = el("modo-atual");
    atual.textContent = NOME_MODO[modo] || modo;
    atual.className = "modo-atual modo-" + modo;
    el("modo-explicacao").textContent = EXPLICACAO_MODO[modo] || "";
    const outro = modo === "PROTECAO" ? "AUDITORIA" : "PROTECAO";
    el("botao-modo").textContent = "Trocar para " + NOME_MODO[outro];
  }

  function desenharSemaforo(risco) {
    const semaforo = el("semaforo");
    semaforo.classList.remove(...CORES);
    semaforo.classList.add("cor-" + risco.cor);
    semaforo.setAttribute("aria-label", "Índice " + risco.indice + " de 100. " + risco.rotulo);
    el("indice").textContent = String(risco.indice);
    el("icone").textContent = ICONES[risco.icone] || "?";
    el("rotulo").textContent = risco.rotulo;
    el("nome-icone").textContent = "Sinal: " + (NOME_ICONE[risco.icone] || risco.icone) + " (" + risco.cor + ")";

    const contadores = risco.contadores || {};
    el("c-eventos").textContent = String(contadores.eventos || 0);
    el("c-expostos").textContent = String(contadores.expostos || 0);
    el("c-neutralizados").textContent = String(contadores.neutralizados || 0);
    el("c-permitidos").textContent = String(contadores.permitidos || 0);

    // Itens iguais (mesma categoria, destino e estado) viram uma linha com a contagem.
    const grupos = new Map();
    (risco.porQue || []).forEach((item) => {
      const chave = (item.categoria || "SEM_CATEGORIA") + " em " + item.destino + ", " + item.estado;
      grupos.set(chave, (grupos.get(chave) || 0) + 1);
    });
    const lista = el("porque");
    lista.replaceChildren();
    if (grupos.size === 0) {
      lista.appendChild(criar("li", "Nenhum evento pesou no índice."));
    }
    grupos.forEach((quantidade, chave) => {
      const sufixo = quantidade > 1 ? " (" + quantidade + " ocorrências)" : "";
      lista.appendChild(criar("li", chave + sufixo));
    });
  }

  function desenharSemServidor() {
    const semaforo = el("semaforo");
    semaforo.classList.remove(...CORES);
    semaforo.classList.add("cor-erro");
    semaforo.setAttribute("aria-label", "Índice indisponível");
    el("indice").textContent = "--";
    el("icone").textContent = "?";
    el("rotulo").textContent = "Sem conexão com o servidor";
    el("nome-icone").textContent = "";
    el("mensagem").textContent = MENSAGEM_SEM_SERVIDOR;
    el("selo").hidden = true;
    el("motivo").hidden = true;
  }

  function desenharMensagem(mensagem) {
    el("mensagem").textContent = mensagem.texto;
    const selo = el("selo");
    selo.hidden = false;
    selo.textContent = mensagem.origem === "IA" ? "IA" : mensagem.selo || "modelo de reserva";
    selo.className = "selo origem-" + mensagem.origem;
    const motivo = el("motivo");
    motivo.hidden = !mensagem.motivo;
    motivo.textContent = mensagem.motivo ? "Motivo: " + mensagem.motivo : "";
  }

  function hora(ts) {
    const data = new Date(ts);
    return Number.isNaN(data.getTime()) ? ts : data.toLocaleTimeString("pt-BR");
  }

  const TEXTO_RASTRO_INICIAL = "Escolha um evento na tabela.";

  // Volta o bloco "Caminho do dado" ao estado inicial: sem título de evento,
  // lista vazia e nenhuma linha marcada.
  function limparRastro() {
    document.querySelectorAll("#corpo-eventos tr.escolhido").forEach((tr) => tr.classList.remove("escolhido"));
    el("rastro-titulo").textContent = TEXTO_RASTRO_INICIAL;
    el("rastro").replaceChildren();
  }

  function mostrarRastro(evento, linha) {
    document.querySelectorAll("#corpo-eventos tr.escolhido").forEach((tr) => tr.classList.remove("escolhido"));
    linha.classList.add("escolhido");
    el("rastro-titulo").textContent =
      "Evento das " + hora(evento.ts) + ": " + (evento.categoria || "SEM_CATEGORIA") + " em " + evento.destino +
      ", " + evento.estado + ". Regra " + evento.regraId + ", ação " + evento.acao + ".";
    const lista = el("rastro");
    lista.replaceChildren();
    (evento.rastro || []).forEach((passo) => {
      let texto = passo.tipo + "  " + passo.descricao;
      if (passo.categorias && passo.categorias.length) {
        texto += " [" + passo.categorias.join(", ") + "]";
      }
      if (passo.valorMascarado !== null && passo.valorMascarado !== undefined) {
        texto += " = " + passo.valorMascarado;
      }
      lista.appendChild(criar("li", texto));
    });
    el("titulo-rastro").scrollIntoView({ block: "nearest" });
  }

  function desenharEventos(eventos) {
    // A tabela vai ser trocada (depois de Reiniciar, Executar ou Trocar modo):
    // o rastro de um evento antigo não pode continuar na tela.
    limparRastro();
    const corpo = el("corpo-eventos");
    corpo.replaceChildren();
    el("sem-eventos").hidden = eventos.length > 0;
    eventos.forEach((evento) => {
      const linha = document.createElement("tr");
      linha.appendChild(criar("td", hora(evento.ts)));
      linha.appendChild(criar("td", evento.cenario || "-"));
      linha.appendChild(criar("td", evento.categoria || "SEM_CATEGORIA"));
      linha.appendChild(criar("td", evento.destino));
      const celulaEstado = criar("td");
      celulaEstado.appendChild(criar("span", evento.estado, "estado estado-" + evento.estado));
      linha.appendChild(celulaEstado);
      linha.appendChild(criar("td", evento.regraId));
      const celulaVer = criar("td");
      const botao = criar("button", "ver", "ver");
      botao.type = "button";
      botao.setAttribute(
        "aria-label",
        "Ver o caminho do evento " + (evento.categoria || "SEM_CATEGORIA") + " em " + evento.destino
      );
      celulaVer.appendChild(botao);
      linha.appendChild(celulaVer);
      // A linha inteira responde ao clique; o botão garante o acesso pelo teclado.
      linha.addEventListener("click", () => mostrarRastro(evento, linha));
      corpo.appendChild(linha);
    });
  }

  function desenharCenarios(cenarios) {
    const area = el("cenarios");
    area.replaceChildren();
    cenarios.forEach((cenario) => {
      const real = cenario.rotulo.indexOf("Real") === 0;
      const botao = criar("button", cenario.id + "  " + cenario.titulo, "acao cenario");
      botao.type = "button";
      botao.appendChild(criar("span", real ? "Real" : "Simulado", "cenario-rotulo"));
      botao.setAttribute("aria-label", cenario.id + ", " + cenario.titulo + ", " + cenario.rotulo);
      botao.addEventListener("click", () =>
        executar(() => chamar("/simulacoes/" + cenario.id, { method: "POST" }), "Executando " + cenario.id + "...")
      );
      area.appendChild(botao);
    });
    bloquear(ocupado);
  }

  // ------------------------------------------------------------------
  // Atualização e ações
  // ------------------------------------------------------------------

  async function traduzir() {
    const pedido = ++pedidoTraducao;
    el("mensagem").textContent = "traduzindo...";
    el("selo").hidden = true;
    el("motivo").hidden = true;
    try {
      const risco = await chamar("/risco-cidadao", null, TEMPO_LIMITE_TRADUCAO_MS);
      if (pedido === pedidoTraducao && risco.mensagem) {
        desenharMensagem(risco.mensagem);
      }
    } catch (erro) {
      if (pedido === pedidoTraducao) {
        el("mensagem").textContent = "Não foi possível montar a mensagem agora. Clique em Atualizar para tentar de novo.";
      }
    }
  }

  async function atualizar() {
    const [saude, risco, eventos] = await Promise.all([
      chamar("/saude"),
      chamar("/risco-cidadao?traduzir=false"),
      chamar("/eventos?limite=50"),
    ]);
    desenharModo(saude.modo);
    desenharSemaforo(risco);
    desenharEventos(eventos.eventos || []);
    // A mensagem vem depois, sem segurar o resto do painel.
    traduzir();
  }

  async function executar(acao, textoStatus) {
    if (ocupado) {
      return;
    }
    bloquear(true);
    mostrarStatus(textoStatus, false);
    try {
      await acao();
      await atualizar();
      mostrarStatus("Pronto.", false);
    } catch (erro) {
      mostrarStatus(MENSAGEM_SEM_SERVIDOR, true);
    } finally {
      bloquear(false);
    }
  }

  function trocarModo() {
    const novo = modoAtual === "PROTECAO" ? "AUDITORIA" : "PROTECAO";
    return executar(
      () => chamar("/modo", { method: "PUT", body: JSON.stringify({ modo: novo }) }),
      "Trocando para " + NOME_MODO[novo] + "..."
    );
  }

  async function iniciar() {
    el("botao-modo").addEventListener("click", trocarModo);
    el("botao-atualizar").addEventListener("click", () => executar(() => Promise.resolve(), "Atualizando..."));
    el("botao-todos").addEventListener("click", () =>
      executar(() => chamar("/simulacoes/todas", { method: "POST" }), "Executando todos os cenários...")
    );
    el("botao-reiniciar").addEventListener("click", () =>
      executar(() => chamar("/reiniciar", { method: "POST" }), "Reiniciando...")
    );
    bloquear(true);
    mostrarStatus("Carregando...", false);
    try {
      const lista = await chamar("/simulacoes");
      desenharCenarios(lista.cenarios || []);
      await atualizar();
      mostrarStatus("", false);
    } catch (erro) {
      desenharSemServidor();
      mostrarStatus(MENSAGEM_SEM_SERVIDOR, true);
    } finally {
      bloquear(false);
    }
  }

  document.addEventListener("DOMContentLoaded", iniciar);
})();
