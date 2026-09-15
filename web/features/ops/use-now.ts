"use client";

import { useEffect, useState } from "react";

/**
 * Un reloj que se re-renderiza cada `intervalMs`. Sin esto, «hace 3 s» se
 * queda congelado hasta el próximo refetch (cada 15 s) en vez de avanzar
 * segundo a segundo — que es lo que hace sentir un mostrador vivo.
 */
export function useNow(intervalMs = 1_000): number {
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    const id = setInterval(() => setNow(Date.now()), intervalMs);
    return () => clearInterval(id);
  }, [intervalMs]);

  return now;
}
