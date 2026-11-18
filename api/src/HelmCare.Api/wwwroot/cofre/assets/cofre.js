// Página do Cofre médico (contrato 1.16, seção 7). O token fica só nesta variável, na memória da
// página: nada de armazenamento local, de sessão ou cookie. Recarregar pede o login de novo.
'use strict';

(function () {
  let token = null;
  let temporizador = null;

  const qrToken = decodeURIComponent(window.location.pathname.split('/').filter(Boolean).pop() || '');

  const el = (id) => document.getElementById(id);

  const mensagensDoAcesso = {
    401: 'Sua sessão terminou. Entre de novo.',
    404: 'QR Code inválido. Confira o código do capacete.',
    429: 'Muitas tentativas. Aguarde um minuto e tente de novo.',
  };

  const mensagensDoMotivo = {
    SOCORRISTA_NAO_VERIFICADO: 'Seu cadastro de socorrista ainda não foi verificado. Sem verificação, a ficha não é liberada.',
    ACESSO_EXPIRADO: 'O acesso a esta ficha expirou.',
  };

  function mostrarMensagem(texto, tipo) {
    const caixa = el('mensagem');
    caixa.textContent = texto;
    caixa.className = tipo === 'info' ? 'mensagem info' : 'mensagem';
    caixa.hidden = !texto;
  }

  function limparFicha() {
    if (temporizador !== null) {
      window.clearInterval(temporizador);
      temporizador = null;
    }

    ['origem', 'tipo-sanguineo', 'alergias', 'condicoes', 'contagem'].forEach((id) => {
      el(id).textContent = '';
    });
    el('contatos').replaceChildren();
    el('secao-ficha').hidden = true;
  }

  function sair(texto, tipo) {
    token = null;
    limparFicha();
    el('senha').value = '';
    el('secao-login').hidden = false;
    mostrarMensagem(texto || '', tipo);
  }

  function formatarTelefone(digitos) {
    if (digitos.length === 11) {
      return '(' + digitos.slice(0, 2) + ') ' + digitos.slice(2, 7) + '-' + digitos.slice(7);
    }

    if (digitos.length === 10) {
      return '(' + digitos.slice(0, 2) + ') ' + digitos.slice(2, 6) + '-' + digitos.slice(6);
    }

    return digitos;
  }

  function textoOuNaoInformado(valor) {
    return valor === null || valor === undefined || valor === '' ? 'Não informado' : valor;
  }

  function iniciarContagem(acessadoEm, expiraEm) {
    // A duração vem do relógio do servidor; a contagem corre no relógio do aparelho.
    const fim = Date.now() + (Date.parse(expiraEm) - Date.parse(acessadoEm));

    const atualizar = () => {
      const restante = Math.max(0, Math.floor((fim - Date.now()) / 1000));
      const minutos = String(Math.floor(restante / 60)).padStart(2, '0');
      const segundos = String(restante % 60).padStart(2, '0');
      el('contagem').textContent = minutos + ':' + segundos;

      if (restante === 0) {
        sair('O tempo de acesso terminou e os dados foram apagados da tela.', 'info');
      }
    };

    atualizar();
    temporizador = window.setInterval(atualizar, 1000);
  }

  function mostrarFicha(ficha) {
    limparFicha();
    el('origem').textContent = ficha.origem === 'SOS_ATIVO' ? 'Acesso por SOS ativo do entregador.' : 'Acesso por emergência manual.';
    el('tipo-sanguineo').textContent = textoOuNaoInformado(ficha.tipoSanguineo);
    el('alergias').textContent = textoOuNaoInformado(ficha.alergias);
    el('condicoes').textContent = textoOuNaoInformado(ficha.condicoesCriticas);

    const lista = el('contatos');
    if (!ficha.contatos || ficha.contatos.length === 0) {
      const item = document.createElement('li');
      item.textContent = 'Nenhum contato cadastrado.';
      lista.appendChild(item);
    } else {
      ficha.contatos.forEach((contato) => {
        const item = document.createElement('li');
        const nome = document.createElement('strong');
        nome.textContent = contato.nome + ' (' + contato.parentesco + ')';
        const ligar = document.createElement('a');
        ligar.href = 'tel:' + contato.telefone;
        ligar.textContent = 'Ligar: ' + formatarTelefone(contato.telefone);
        item.append(nome, document.createElement('br'), ligar);
        lista.appendChild(item);
      });
    }

    el('secao-login').hidden = true;
    el('secao-ficha').hidden = false;
    mostrarMensagem('');
    iniciarContagem(ficha.acessadoEm, ficha.expiraEm);
  }

  async function lerProblema(resposta) {
    try {
      return await resposta.json();
    } catch {
      return null;
    }
  }

  async function abrirFicha() {
    const resposta = await fetch('/api/v1/cofre/' + encodeURIComponent(qrToken) + '/acesso', {
      method: 'POST',
      headers: { Authorization: 'Bearer ' + token },
      cache: 'no-store',
      referrerPolicy: 'no-referrer',
    });

    if (resposta.status === 200) {
      mostrarFicha(await resposta.json());
      return;
    }

    if (resposta.status === 403) {
      const problema = await lerProblema(resposta);
      const motivo = problema && problema.motivo;
      sair(mensagensDoMotivo[motivo] || 'Esta conta não tem acesso ao Cofre.');
      return;
    }

    sair(mensagensDoAcesso[resposta.status] || 'Não foi possível abrir a ficha agora. Tente de novo.');
  }

  async function entrar(evento) {
    evento.preventDefault();
    const botao = el('botao-entrar');
    botao.disabled = true;
    mostrarMensagem('');

    try {
      const resposta = await fetch('/api/v1/socorristas/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: el('email').value, senha: el('senha').value }),
        cache: 'no-store',
        referrerPolicy: 'no-referrer',
      });

      el('senha').value = '';

      if (resposta.status === 200) {
        const login = await resposta.json();
        token = login.accessToken;
        await abrirFicha();
      } else if (resposta.status === 401) {
        mostrarMensagem('E-mail ou senha inválidos.');
      } else if (resposta.status === 429) {
        mostrarMensagem(mensagensDoAcesso[429]);
      } else {
        mostrarMensagem('Preencha o e-mail e a senha.');
      }
    } catch {
      mostrarMensagem('Sem conexão com o HelmCare. Ligue para o SAMU 192 se precisar.');
    } finally {
      botao.disabled = false;
    }
  }

  document.addEventListener('DOMContentLoaded', () => {
    el('form-login').addEventListener('submit', entrar);
    el('botao-sair').addEventListener('click', () => sair('Você saiu. Os dados foram apagados da tela.', 'info'));

    if (!qrToken) {
      mostrarMensagem(mensagensDoAcesso[404]);
    }
  });
})();
