"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { ChevronsUpDown, LogOut, Monitor, Moon, Sun } from "lucide-react";
import { useTheme } from "next-themes";

import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
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
import { roleLabel } from "@/features/auth/roles";
import type { CurrentUser } from "@/features/auth/use-current-user";
import { authClient } from "@/lib/auth/client";
import { formatUserName } from "@/lib/format";

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
  const router = useRouter();
  const [signingOut, setSigningOut] = useState(false);

  const correo = user.email ?? "usuario";
  const nombre = formatUserName(correo);
  const rol = roleLabel(user.role);

  async function signOut() {
    setSigningOut(true);
    await authClient.signOut();
    // `refresh` limpia la caché del layout de servidor; el middleware redirige a /login.
    router.replace("/login");
    router.refresh();
  }

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
                  {correo}
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

            <DropdownMenuSeparator />

            <DropdownMenuItem
              disabled={signingOut}
              onClick={(event) => {
                // No cerrar el menú antes de que la navegación arranque.
                event.preventDefault();
                void signOut();
              }}
            >
              <LogOut aria-hidden />
              Cerrar sesión
            </DropdownMenuItem>
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
