"use client";

import { useState } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { NuqsAdapter } from "nuqs/adapters/next/app";
import { ThemeProvider } from "next-themes";
import { ReactQueryDevtools } from "@tanstack/react-query-devtools";

import { Toaster } from "@/components/ui/sonner";
import { TooltipProvider } from "@/components/ui/tooltip";
import { ApiError } from "@/lib/api/problem";

/**
 * Los proveedores de cliente de la aplicación.
 *
 * El `QueryClient` se crea con `useState` y no a nivel de módulo: en el servidor, un
 * cliente de módulo se compartiría entre peticiones de personas distintas, y la caché de
 * una acabaría en la pantalla de la otra.
 */
export function Providers({ children }: { children: React.ReactNode }) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            /**
             * Un minuto. En un mostrador los datos cambian por lo que hace la propia
             * persona —y eso invalida la caché explícitamente—, no por otros: no vale
             * consultar de nuevo cada vez que la ventana recupera el foco.
             */
            staleTime: 60_000,
            refetchOnWindowFocus: false,

            /**
             * No se reintenta lo que no va a mejorar solo.
             *
             * Un 403 porque el rol no alcanza o un 404 no cambian por insistir. Y tampoco
             * los errores del propio proxy —`Proxy.*`—, que significan que la API no
             * responde: reintentarlos con espera exponencial convierte un fallo de
             * configuracion en veinte segundos de aparente lentitud antes de mostrar el
             * mensaje. Eso ya paso una vez con la redireccion a https.
             *
             * Lo que si se reintenta es un 5xx que venga de la API, que puede ser un
             * tropiezo pasajero.
             */
            retry: (failureCount, error) => {
              if (error instanceof ApiError) {
                if (error.status < 500) return false;

                if (error.problem.code?.startsWith("Proxy.")) return false;
              }

              return failureCount < 2;
            },
          },
          mutations: {
            // Nunca: una venta que se reintenta sola se puede cobrar dos veces.
            retry: false,
          },
        },
      }),
  );

  return (
    <QueryClientProvider client={queryClient}>
      {/* `class` y no el atributo por defecto: el CSS declara la variante oscura como
          `&:is(.dark *)`. Y sin `disableTransitionOnChange` el cambio de tema arrastra
          cada transición de color de la pantalla y se ve como un barrido. */}
      <ThemeProvider
        attribute="class"
        defaultTheme="system"
        enableSystem
        disableTransitionOnChange
      >
        <NuqsAdapter>
          <TooltipProvider>
            {children}
            <Toaster position="bottom-right" />
          </TooltipProvider>
        </NuqsAdapter>
      </ThemeProvider>
      <ReactQueryDevtools initialIsOpen={false} />
    </QueryClientProvider>
  );
}
