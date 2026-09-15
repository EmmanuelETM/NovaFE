import { cn } from "@/lib/utils";

import type { Severity } from "./hero";

const DOT_COLOR: Record<Severity, string> = {
  ok: "bg-primary",
  warn: "bg-amber-500",
  crit: "bg-destructive",
};

/** El punto que titila junto al titular, coloreado por la severidad del héroe. */
export function StatusDot({ severity }: { severity: Severity }) {
  return (
    <span className="relative inline-flex size-2.5" aria-hidden="true">
      <span
        className={cn(
          "absolute inline-flex h-full w-full animate-ping rounded-full opacity-60",
          DOT_COLOR[severity],
        )}
      />
      <span
        className={cn(
          "relative inline-flex size-2.5 rounded-full",
          DOT_COLOR[severity],
        )}
      />
    </span>
  );
}
