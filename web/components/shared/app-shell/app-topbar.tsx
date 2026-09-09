"use client";

import { usePathname } from "next/navigation";

import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from "@/components/ui/breadcrumb";
import { Separator } from "@/components/ui/separator";
import { SidebarTrigger } from "@/components/ui/sidebar";
import { NAVIGATION, findNavItem } from "@/lib/navigation";

/**
 * La barra superior: dónde estoy y cómo colapso el sidebar.
 *
 * Es una **miga de pan**, no un título. El `h1` de cada pantalla vive en la pantalla —
 * aquí lo que se resuelve es la orientación: con el sidebar colapsado a iconos, el grupo
 * («Catálogo») es lo único que dice en qué parte de la aplicación estás.
 *
 * No se queda fija con `sticky`: quien tiene el scroll es el panel de contenido, así que
 * esta barra está quieta por construcción. Con `sticky` se pegaría al borde de la ventana y
 * no al del panel —que con `variant="inset"` está dos píxeles más adentro—, y se vería
 * flotando sobre la esquina redondeada.
 */
export function AppTopbar() {
  const pathname = usePathname();

  const actual = findNavItem(pathname);
  const grupo = NAVIGATION.find((section) =>
    section.items.some((item) => item.href === actual?.href),
  );

  return (
    <header className="flex h-14 shrink-0 items-center gap-2 border-b px-4">
      <SidebarTrigger className="-ml-1" />
      <Separator orientation="vertical" className="mr-1 !h-4" />

      <Breadcrumb>
        <BreadcrumbList>
          {grupo && (
            // Los grupos no son pantallas, así que no enlazan a ninguna parte. En angosto
            // se va: con poco ancho el nombre de la pantalla vale más que su categoría.
            <>
              <BreadcrumbItem className="hidden sm:block">
                {grupo.label}
              </BreadcrumbItem>
              <BreadcrumbSeparator className="hidden sm:block" />
            </>
          )}
          <BreadcrumbItem>
            <BreadcrumbPage>{actual?.label ?? "Inicio"}</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>
    </header>
  );
}
