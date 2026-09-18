import type { ReactNode } from "react";
import { Boxes } from "lucide-react";

import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";

interface AuthCardProps {
  title: string;
  description: string;
  children: ReactNode;
}

/**
 * Encabezado compartido por las pantallas de acceso (login, registro, olvidé mi
 * contraseña, restablecer contraseña): mismo logo, mismo `Card`.
 */
export function AuthCard({ title, description, children }: AuthCardProps) {
  return (
    <Card className="w-full max-w-sm">
      <CardHeader className="items-center text-center">
        <div className="bg-primary text-primary-foreground mx-auto mb-2 flex size-12 items-center justify-center rounded-xl pb-2">
          <Boxes className="size-6" aria-hidden />
        </div>
        <CardTitle className="text-lg">{title}</CardTitle>
        <CardDescription>{description}</CardDescription>
      </CardHeader>

      <CardContent className="flex flex-col gap-3">{children}</CardContent>
    </Card>
  );
}
