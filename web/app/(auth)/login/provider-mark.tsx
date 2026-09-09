import type { SocialProvider } from "@/lib/auth/social";

/**
 * Marcas de los providers de OAuth. Lucide ya no trae iconos de marca, así que
 * van como SVG inline (paths oficiales, `currentColor` salvo el de Microsoft que
 * es multicolor por diseño).
 */
export function ProviderMark({ provider }: { provider: SocialProvider }) {
  if (provider === "github") {
    return (
      <svg
        viewBox="0 0 24 24"
        className="size-4"
        fill="currentColor"
        aria-hidden
      >
        <path d="M12 .5C5.37.5 0 5.87 0 12.5c0 5.3 3.44 9.8 8.2 11.39.6.11.82-.26.82-.58 0-.29-.01-1.04-.02-2.05-3.34.73-4.04-1.61-4.04-1.61-.55-1.39-1.33-1.76-1.33-1.76-1.09-.74.08-.73.08-.73 1.2.08 1.84 1.24 1.84 1.24 1.07 1.83 2.81 1.3 3.5.99.11-.78.42-1.3.76-1.6-2.67-.3-5.47-1.33-5.47-5.93 0-1.31.47-2.38 1.24-3.22-.13-.3-.54-1.52.11-3.18 0 0 1.01-.32 3.3 1.23a11.5 11.5 0 0 1 6 0c2.29-1.55 3.3-1.23 3.3-1.23.65 1.66.24 2.88.12 3.18.77.84 1.23 1.91 1.23 3.22 0 4.61-2.8 5.63-5.48 5.92.43.37.81 1.1.81 2.22 0 1.61-.01 2.9-.01 3.29 0 .32.21.7.82.58A12 12 0 0 0 24 12.5C24 5.87 18.63.5 12 .5Z" />
      </svg>
    );
  }

  if (provider === "google") {
    return (
      <svg viewBox="0 0 24 24" className="size-4" aria-hidden>
        <path
          fill="#4285F4"
          d="M23.06 12.25c0-.85-.08-1.67-.22-2.45H12v4.64h6.2a5.3 5.3 0 0 1-2.3 3.48v2.9h3.72c2.18-2 3.44-4.96 3.44-8.57Z"
        />
        <path
          fill="#34A853"
          d="M12 24c3.1 0 5.7-1.03 7.6-2.78l-3.72-2.9c-1.03.7-2.35 1.1-3.88 1.1-2.98 0-5.5-2.01-6.4-4.72H1.75v2.99A11.99 11.99 0 0 0 12 24Z"
        />
        <path
          fill="#FBBC05"
          d="M5.6 14.7a7.2 7.2 0 0 1 0-4.6V7.11H1.75a12 12 0 0 0 0 10.78L5.6 14.7Z"
        />
        <path
          fill="#EA4335"
          d="M12 4.77c1.68 0 3.2.58 4.4 1.72l3.3-3.3C17.7 1.2 15.1 0 12 0A11.99 11.99 0 0 0 1.75 7.11L5.6 10.1C6.5 7.39 9.02 4.77 12 4.77Z"
        />
      </svg>
    );
  }

  return (
    <svg viewBox="0 0 24 24" className="size-4" aria-hidden>
      <path fill="#F25022" d="M1 1h10.5v10.5H1z" />
      <path fill="#7FBA00" d="M12.5 1H23v10.5H12.5z" />
      <path fill="#00A4EF" d="M1 12.5h10.5V23H1z" />
      <path fill="#FFB900" d="M12.5 12.5H23V23H12.5z" />
    </svg>
  );
}
