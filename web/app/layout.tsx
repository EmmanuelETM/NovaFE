import type { Metadata } from "next";
import { Geist, Geist_Mono, Inter } from "next/font/google";
import "./globals.css";
import { cn } from "@/lib/utils";
import { Providers } from "./providers";

const inter = Inter({ subsets: ["latin"], variable: "--font-sans" });

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: "Aplicación",
  description: "Descripción de la aplicación",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html
      // La aplicación es en español, y el idioma declarado es lo que usan el lector de
      // pantalla para pronunciar y el navegador para ofrecer traducción.
      lang="es"
      // next-themes escribe la clase del tema antes de pintar, así que el HTML del
      // servidor y el del cliente difieren a propósito en este nodo.
      suppressHydrationWarning
      className={cn(
        "h-full",
        "antialiased",
        geistSans.variable,
        geistMono.variable,
        "font-sans",
        inter.variable,
      )}
    >
      <body className="flex min-h-full flex-col">
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}
