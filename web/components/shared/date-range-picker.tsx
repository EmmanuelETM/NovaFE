"use client";

import { useState } from "react";
import { CalendarIcon } from "lucide-react";
import type { DateRange } from "react-day-picker";

import { Button } from "@/components/ui/button";
import { Calendar } from "@/components/ui/calendar";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";
import { dateOnlyString } from "@/lib/format/date";

const LABEL_FORMAT = new Intl.DateTimeFormat("es-DO", {
  timeZone: "UTC",
  dateStyle: "medium",
});

interface DateRangePickerProps {
  /** Día local `YYYY-MM-DD`, igual que el resto de la app (`today()`/`addDays()`). */
  from: string;
  to: string;
  onChange: (range: { from: string; to: string }) => void;
}

/**
 * Selector de rango de fechas — primero de este tipo en la app, vive acá
 * (no dentro de una feature) porque cualquier otro reporte futuro también
 * lo va a necesitar. Trabaja en días locales `YYYY-MM-DD` hacia afuera
 * (mismo formato que `today()`/`addDays()`); el `Date` nativo solo vive
 * adentro, porque es lo que pide `react-day-picker`.
 */
export function DateRangePicker({ from, to, onChange }: DateRangePickerProps) {
  const [open, setOpen] = useState(false);

  // El día es un DateOnly sin hora — se ancla a mediodía UTC para que no
  // se corra un día al construir el `Date` (mismo criterio que
  // `formatCalendarDate`).
  const asDate = (day: string) => new Date(`${day}T12:00:00Z`);
  const range: DateRange = { from: asDate(from), to: asDate(to) };

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger
        render={<Button variant="outline" className="h-9 gap-2" />}
      >
        <CalendarIcon className="size-4" aria-hidden />
        <span>
          {LABEL_FORMAT.format(asDate(from))} –{" "}
          {LABEL_FORMAT.format(asDate(to))}
        </span>
      </PopoverTrigger>

      <PopoverContent className="w-auto p-0" align="start">
        <Calendar
          mode="range"
          selected={range}
          defaultMonth={asDate(from)}
          numberOfMonths={2}
          onSelect={(next) => {
            if (!next?.from || !next.to) return;
            onChange({
              from: dateOnlyString(next.from),
              to: dateOnlyString(next.to),
            });
            setOpen(false);
          }}
        />
      </PopoverContent>
    </Popover>
  );
}
