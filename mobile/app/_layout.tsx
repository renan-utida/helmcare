import { Stack } from 'expo-router';
import { SQLiteProvider } from 'expo-sqlite';
import { StatusBar } from 'expo-status-bar';
import { Suspense } from 'react';

import { migrarBanco } from '@/banco/migracoes';
import { Carregando } from '@/componentes/Carregando';
import { SessaoProvider } from '@/contextos/SessaoContext';
import { TemaDoSistema, TemaProvider, useTema } from '@/contextos/TemaContext';
import { TurnoProvider } from '@/contextos/TurnoContext';

/** Nome do arquivo do banco local no aparelho. */
const NOME_BANCO = 'helmcare.db';

/**
 * Pilha raiz com as cores do tema. Fica num componente separado porque precisa
 * do `useTema()`, que só existe dentro do `TemaProvider`.
 */
function PilhaRaiz() {
  const { cores, esquema } = useTema();
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
        <Stack.Screen name="cadastro" options={{ title: 'Cadastro' }} />
        <Stack.Screen name="(abas)" options={{ headerShown: false }} />
        <Stack.Screen name="ficha-medica" options={{ presentation: 'modal', title: 'Ficha médica' }} />
        <Stack.Screen name="contato" options={{ presentation: 'modal', title: 'Contato de emergência' }} />
        <Stack.Screen name="perfis" options={{ presentation: 'modal', title: 'Perfis neste aparelho' }} />
        {/* Alerta de queda: tela cheia, sem cabeçalho e sem gesto de fechar (o voltar do
            Android é bloqueado pela própria tela enquanto o alerta estiver em andamento). */}
        <Stack.Screen
          name="alerta"
          options={{ presentation: 'fullScreenModal', headerShown: false, gestureEnabled: false }}
        />
        <Stack.Screen name="ocorrencia/[id]" options={{ title: 'Ocorrência' }} />
      </Stack>
    </>
  );
}

/**
 * Layout raiz: abre o banco local (migrações no `onInit`, antes de qualquer tela),
 * depois o tema, a sessão, o turno do perfil ativo e a pilha de telas.
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
              <PilhaRaiz />
            </TurnoProvider>
          </SessaoProvider>
        </TemaProvider>
      </SQLiteProvider>
    </Suspense>
  );
}
