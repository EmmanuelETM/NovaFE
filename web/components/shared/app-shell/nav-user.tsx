"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import {
  Bell,
  LogOut,
  Monitor,
  Moon,
  Sun,
  User as UserIcon,
} from "lucide-react";
import { useTheme } from "next-themes";

import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
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
import { roleLabel } from "@/features/auth/roles";
import type { CurrentUser } from "@/features/auth/use-current-user";
import { authClient } from "@/lib/auth/client";
import { formatUserName } from "@/lib/format";

const TEMAS = [
  { value: "light", label: "Claro", icon: Sun },
  { value: "dark", label: "Oscuro", icon: Moon },
  { value: "system", label: "Sistema", icon: Monitor },
] as const;

/** Lo que todavía no tiene pantalla propia — mismo criterio de "pronto" que el sidebar. */
const PENDING_ITEMS = [
  { icon: UserIcon, label: "Cuenta" },
  { icon: Bell, label: "Notificaciones" },
] as const;

interface NavUserProps {
  user: CurrentUser;
}

/**
 * Quién está usando el sistema — a propósito en la barra **superior**, no en
 * el sidebar: es lo personal (cuenta, notificaciones, apariencia, cerrar
 * sesión), separado de lo del contribuyente (`NavTenant`, al pie del
 * sidebar). Mismo criterio que GitHub/Linear/Vercel: el avatar propio va
 * arriba a la derecha; lo de la organización vive en su propio lugar.
 */
export function NavUser({ user }: NavUserProps) {
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
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <Button
            variant="ghost"
            size="icon"
            className="rounded-full"
            aria-label={`${nombre} · ${rol}`}
          />
        }
      >
        <Avatar className="size-8">
          <AvatarFallback className="text-xs">
            {initials(nombre)}
          </AvatarFallback>
        </Avatar>
      </DropdownMenuTrigger>

      <DropdownMenuContent className="min-w-56" align="end" sideOffset={8}>
        <DropdownMenuGroup>
          {/* El correo, no solo el nombre: es lo que se le dice a TI cuando algo de
              permisos no cuadra, y en desarrollo es la única forma de notar que se
              está actuando como el usuario del appsettings. */}
          <DropdownMenuLabel className="font-normal">
            <span className="block truncate text-sm font-medium">{nombre}</span>
            <span className="text-muted-foreground block truncate text-xs">
              {correo}
            </span>
          </DropdownMenuLabel>
        </DropdownMenuGroup>

        <DropdownMenuSeparator />

        <DropdownMenuGroup>
          {PENDING_ITEMS.map((item) => (
            <DropdownMenuItem key={item.label} disabled>
              <item.icon aria-hidden />
              {item.label}
              <span className="text-muted-foreground ml-auto text-xs font-normal">
                pronto
              </span>
            </DropdownMenuItem>
          ))}
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
              <DropdownMenuRadioItem key={opcion.value} value={opcion.value}>
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
