"use client";

import { useEffect, useState } from "react";

/**
 * El valor de antes, hasta que pare de cambiar.
 *
 * Existe para que la cotización del punto de venta no salga en cada tecla: el carrito
 * cambia mientras se teclea una cantidad, y sin esto cada dígito sería una petición.
 *
 * Aquí `setState` dentro de un efecto **es** lo correcto: el temporizador es un sistema
 * externo, y el efecto está sincronizando con él. La regla del lint apunta a los efectos
 * que copian estado en cada render, que es otra cosa.
 */
export function useDebouncedValue<T>(value: T, delay = 150): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delay);

    return () => clearTimeout(timer);
  }, [value, delay]);

  return debounced;
}
