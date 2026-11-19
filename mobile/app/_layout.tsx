import { Stack } from 'expo-router';
import { SQLiteProvider } from 'expo-sqlite';
import { StatusBar } from 'expo-status-bar';
import { Suspense } from 'react';

import { migrarBanco } from '@/banco/migracoes';
import { Carregando } from '@/componentes/Carregando';
import { ChamadosProvider } from '@/contextos/ChamadosContext';
import { SessaoProvider } from '@/contextos/SessaoContext';
import { TemaDoSistema, TemaProvider, useTema } from '@/contextos/TemaContext';
import { TurnoProvider } from '@/contextos/TurnoContext';
import { useNotificacoesTocadas } from '@/hooks/useNotificacoesTocadas';

/** Nome do arquivo do banco local no aparelho. */
const NOME_BANCO = 'helmcare.db';

/**
 * Pilha raiz com as cores do tema. Fica num componente separado porque precisa
 * do `useTema()`, que só existe dentro do `TemaProvider`.
 */
function PilhaRaiz() {
  const { cores, esquema } = useTema();
  // Notificações locais: prepara o canal e abre a tela certa quando a pessoa toca numa delas.
  useNotificacoesTocadas();
  return (
    <>
      <StatusBar style={esquema === 'escuro' ? 'light' : 'dark'} />
      <Stack
        screenOptions={{
          headerStyle: { backgroundColor: cores.superficie },
          headerTintColor: cores.texto,
          headerTitleStyle: { color: cores.texto },
          contentStyle: { backgroundColor: cores.fundo },
          // Texto do botão voltar sempre "Voltar", para o leitor de tela.
          headerBackButtonDisplayMode: 'minimal',
          headerBackTitle: 'Voltar',
        }}
      >
        <Stack.Screen name="index" options={{ headerShown: false }} />
        {/* Login: sem cabeçalho na entrada; com cabeçalho e voltar ao adicionar conta ou ao
            entrar de novo no meio de um alerta (a própria tela ajusta o título). */}
        <Stack.Screen name="login" options={{ title: 'Entrar' }} />
        <Stack.Screen name="cadastro" options={{ title: 'Cadastro' }} />
        <Stack.Screen name="(abas)" options={{ headerShown: false }} />
        <Stack.Screen name="ficha-medica" options={{ presentation: 'modal', title: 'Ficha médica' }} />
        <Stack.Screen name="contato" options={{ presentation: 'modal', title: 'Contato de emergência' }} />
        <Stack.Screen name="contas" options={{ presentation: 'modal', title: 'Contas neste aparelho' }} />
        <Stack.Screen name="telefone" options={{ presentation: 'modal', title: 'Trocar telefone' }} />
        <Stack.Screen name="contatos" options={{ title: 'Contatos de emergência' }} />
        <Stack.Screen name="qr-capacete" options={{ title: 'QR Code do seu capacete' }} />
        <Stack.Screen name="acessos-ficha" options={{ presentation: 'modal', title: 'Quem acessou minha ficha' }} />
        {/* Alerta de queda: tela cheia, sem cabeçalho e sem gesto de fechar (o voltar do
            Android é bloqueado pela própria tela enquanto o alerta estiver em andamento). */}
        <Stack.Screen
          name="alerta"
          options={{ presentation: 'fullScreenModal', headerShown: false, gestureEnabled: false }}
        />
        <Stack.Screen name="ocorrencia/[id]" options={{ title: 'Ocorrência' }} />
        {/* Turno: telas da pilha abertas pela Início, com a seta de voltar padrão. */}
        <Stack.Screen name="turno/novo" options={{ title: 'Começar turno' }} />
        <Stack.Screen name="turno/ativo" options={{ title: 'Turno em andamento' }} />
      </Stack>
    </>
  );
}

/**
 * Layout raiz: abre o banco local (migrações no `onInit`, antes de qualquer tela),
 * depois o tema, a sessão, o turno e a rede de voluntários do perfil ativo e a pilha de telas.
 * Enquanto o banco abre, o `Suspense` mostra a abertura com o logo (no tema do celular,
 * porque a escolha dos Ajustes ainda não pode ser lida).
 */
export default function LayoutRaiz() {
  return (
    <Suspense
      fallback={
        <TemaDoSistema>
          <Carregando comLogo />
        </TemaDoSistema>
      }
    >
      <SQLiteProvider databaseName={NOME_BANCO} onInit={migrarBanco} useSuspense>
        <TemaProvider>
          <SessaoProvider>
            <TurnoProvider>
              <ChamadosProvider>
                <PilhaRaiz />
              </ChamadosProvider>
            </TurnoProvider>
          </SessaoProvider>
        </TemaProvider>
      </SQLiteProvider>
    </Suspense>
  );
}
