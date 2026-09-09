"use client";

import { ChevronsUpDown, Monitor, Moon, Sun } from "lucide-react";
import { useTheme } from "next-themes";

import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  useSidebar,
} from "@/components/ui/sidebar";
import { formatUserName } from "@/lib/format";
import type { CurrentUser } from "@/features/auth/use-current-user";

const TEMAS = [
  { value: "light", label: "Claro", icon: Sun },
  { value: "dark", label: "Oscuro", icon: Moon },
  { value: "system", label: "Sistema", icon: Monitor },
] as const;

interface NavUserProps {
  user: CurrentUser;
}

/**
 * Quién está usando el sistema, al pie del sidebar.
 *
 * Mostrar la identidad no es decoración cuando la máquina es compartida: cada cambio queda
 * firmado con quien esté en sesión, y que se vea a simple vista de quién es la firma evita
 * el error de trabajar media hora como otra persona.
 *
 * El menú trae el selector de tema. Si la aplicación llega a tener cierre de sesión, este
 * es su sitio.
 */
export function NavUser({ user }: NavUserProps) {
  const { isMobile } = useSidebar();
  const { theme, setTheme } = useTheme();

  // El nombre real si lo hay; si no, el corto. El correo completo va dentro del menú: aquí
  // no cabe, y un correo institucional truncado a la mitad no identifica a nadie.
  const nombre = user.displayName ?? formatUserName(user.userName);
  const rol = user.roleLabel ?? "Sin rol";

  return (
    <SidebarMenu>
      <SidebarMenuItem>
        <DropdownMenu>
          <DropdownMenuTrigger
            render={
              <SidebarMenuButton size="lg" tooltip={`${nombre} · ${rol}`}>
                <Avatar className="size-7 rounded-lg">
                  <AvatarFallback className="rounded-lg text-xs">
                    {initials(nombre)}
                  </AvatarFallback>
                </Avatar>
                <div className="grid flex-1 text-left leading-tight">
                  <span className="truncate text-sm font-medium">{nombre}</span>
                  <span className="text-muted-foreground truncate text-xs">
                    {rol}
                  </span>
                </div>
                <ChevronsUpDown className="ml-auto" aria-hidden />
              </SidebarMenuButton>
            }
          />

          <DropdownMenuContent
            className="w-(--anchor-width) min-w-56"
            side={isMobile ? "top" : "right"}
            align="end"
            sideOffset={8}
          >
            <DropdownMenuGroup>
              {/* El correo, no solo el nombre: es lo que se le dice a TI cuando algo de
                  permisos no cuadra, y en desarrollo es la única forma de notar que se
                  está actuando como el usuario del appsettings. */}
              <DropdownMenuLabel className="font-normal">
                <span className="block truncate text-sm font-medium">
                  {nombre}
                </span>
                <span className="text-muted-foreground block truncate text-xs">
                  {user.userName}
                </span>
              </DropdownMenuLabel>
            </DropdownMenuGroup>

            <DropdownMenuSeparator />

            <DropdownMenuGroup>
              <DropdownMenuLabel className="text-muted-foreground text-xs">
                Apariencia
              </DropdownMenuLabel>
              <DropdownMenuRadioGroup
                value={theme ?? "system"}
                onValueChange={(value) => setTheme(String(value))}
              >
                {TEMAS.map((opcion) => (
                  <DropdownMenuRadioItem
                    key={opcion.value}
                    value={opcion.value}
                  >
                    <opcion.icon aria-hidden />
                    {opcion.label}
                  </DropdownMenuRadioItem>
                ))}
              </DropdownMenuRadioGroup>
            </DropdownMenuGroup>
          </DropdownMenuContent>
        </DropdownMenu>
      </SidebarMenuItem>
    </SidebarMenu>
  );
}

/**
 * Las iniciales para el avatar.
 *
 * Un nombre da dos letras; un correo da la primera del usuario. Nunca devuelve vacío
 * porque un avatar en blanco parece un error de carga.
 */
function initials(nombre: string): string {
  const palabras = nombre
    .replace(/@.*$/, "")
    .split(/[\s.]+/)
    .filter(Boolean);

  const letras = palabras
    .slice(0, 2)
    .map((palabra) => palabra.charAt(0))
    .join("");

  return letras.toUpperCase() || "?";
}
