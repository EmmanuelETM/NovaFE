"use client";

import type { ReactNode } from "react";

import type { RoleLevel } from "./roles";
import { useCan } from "./use-current-user";

interface CanProps {
  /** Nivel mínimo. Los roles son jerárquicos, así que «supervisor» incluye al administrador. */
  role: RoleLevel;
  children: ReactNode;
  /** Qué mostrar cuando no alcanza. Por defecto, nada. */
  fallback?: ReactNode;
}

/**
 * Muestra a sus hijos solo si el usuario alcanza el rol.
 *
 * Mientras no se sabe no muestra nada, para que los botones no aparezcan de golpe medio
 * segundo después de cargar la pantalla.
 */
export function Can({ role, children, fallback = null }: CanProps) {
  const allowed = useCan(role);

  if (allowed === undefined) return null;

  return allowed ? children : fallback;
}
