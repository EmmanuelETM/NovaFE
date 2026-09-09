import "server-only";

import { Resend } from "resend";

import { env } from "@/lib/env";

interface Email {
  to: string;
  subject: string;
  html: string;
}

/**
 * Envío de correo transaccional (verificación de email, reset de contraseña).
 *
 * **Contrato**: el resto del código llama `sendEmail(...)`. Hoy es Resend;
 * cambiar a Azure Communication Services / SES es reescribir solo esta función.
 *
 * Sin `RESEND_API_KEY` (desarrollo), el correo se escribe a la consola en vez de
 * enviarse — así el flujo de verificación/reset se puede probar en local sin
 * cuenta de Resend. El enlace sale en el log del servidor de Next.
 */
export async function sendEmail({ to, subject, html }: Email): Promise<void> {
  if (!env.RESEND_API_KEY) {
    console.info(
      `[email:dev] to=${to}\nsubject=${subject}\n${html.replace(/<[^>]+>/g, " ").trim()}`,
    );
    return;
  }

  const resend = new Resend(env.RESEND_API_KEY);
  const { error } = await resend.emails.send({
    from: env.EMAIL_FROM,
    to,
    subject,
    html,
  });

  if (error) {
    throw new Error(`Resend rechazó el correo a ${to}: ${error.message}`);
  }
}
