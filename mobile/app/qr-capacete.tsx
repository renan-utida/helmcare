import { router } from 'expo-router';
import { useState } from 'react';
import { Linking, useWindowDimensions, View } from 'react-native';
import QRCode from 'react-native-qrcode-svg';

import { Aviso } from '@/componentes/Aviso';
import { Botao } from '@/componentes/Botao';
import { EstadoVazio } from '@/componentes/EstadoVazio';
import { Tela } from '@/componentes/Tela';
import { Texto } from '@/componentes/Texto';
import { useTema } from '@/contextos/TemaContext';
import { retirarDoFluxo } from '@/utils/memoriaDoFluxo';

/** Lado máximo do QR, em dp (cabe com folga na tela e é fácil de fotografar). */
const LADO_MAXIMO_QR = 280;

/**
 * "QR Code do seu capacete", aberta logo depois do 201 do `POST /dispositivos` (no cadastro e no
 * Perfil). O QR leva o endereço completo da página do Cofre (a `urlCofre`, que vem relativa, com a
 * origem da URL da API em uso). Esse endereço tem o `qrToken`, que é credencial (seção 9): chega
 * pela memória do fluxo (nunca pela rota), fica só no estado desta tela e some ao sair. O app não
 * guarda o QR em lugar nenhum: a pessoa fotografa ou imprime agora.
 */
export default function QrCapacete() {
  const { cores, espaco } = useTema();
  const { width } = useWindowDimensions();
  // Lido uma vez ao abrir: a memória do fluxo apaga o valor na leitura.
  const [qr] = useState(() => retirarDoFluxo('qrCapacete'));
  const [erroNavegador, setErroNavegador] = useState(false);
  const lado = Math.min(width - espaco.medio * 4, LADO_MAXIMO_QR);

  if (!qr) {
    return (
      <Tela>
        <EstadoVazio
          titulo="O QR Code só aparece logo depois de vincular o capacete."
          mensagem="O app não guarda este código. Para um QR novo, fale com o suporte."
          icone="qr-code-outline"
        />
        <Botao titulo="Voltar" icone="arrow-back" aoPressionar={() => router.back()} />
      </Tela>
    );
  }

  async function testarNoNavegador(url: string) {
    try {
      await Linking.openURL(url);
    } catch {
      // Nada vai para log: o endereço tem o qrToken.
      setErroNavegador(true);
    }
  }

  return (
    <Tela>
      <Texto variante="titulo">{`Capacete ${qr.codigo} vinculado`}</Texto>
      <View
        accessible
        accessibilityRole="image"
        accessibilityLabel={`QR Code do capacete ${qr.codigo}. Ele abre a página do Cofre médico para o socorrista.`}
        style={{ alignSelf: 'center', padding: espaco.medio, backgroundColor: cores.qrFundo }}
      >
        <QRCode value={qr.urlCofreCompleta} size={lado} color={cores.qrModulo} backgroundColor={cores.qrFundo} ecl="M" />
      </View>
      <Texto>Só socorristas verificados conseguem abrir a sua ficha, e só durante a emergência.</Texto>
      <Aviso
        tipo="atencao"
        mensagem="Fotografe ou imprima agora e cole no capacete: o app não guarda este código."
      />
      <Botao
        titulo="Testar no navegador"
        variante="secundaria"
        icone="globe"
        aoPressionar={() => void testarNoNavegador(qr.urlCofreCompleta)}
        accessibilityHint="Abre a página do Cofre no navegador, como o socorrista veria"
      />
      {erroNavegador ? <Aviso tipo="erro" mensagem="Não deu para abrir o navegador neste celular." /> : null}
      <Botao titulo="Pronto, já guardei" icone="checkmark" aoPressionar={() => router.back()} />
    </Tela>
  );
}
