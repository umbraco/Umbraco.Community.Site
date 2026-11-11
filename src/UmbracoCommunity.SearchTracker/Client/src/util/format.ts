export function formatDay(isoDay: string): string {
  const [year, month, day] = isoDay.split("-");
  return year && month && day ? `${day}-${month}-${year}` : isoDay;
}

export function toIsoDay(date: Date): string {
  return date.toISOString().slice(0, 10);
}

export function rangeForDays(days: number, today: Date = new Date()): { from: string; to: string } {
  const end = new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), today.getUTCDate()));
  const start = new Date(end);
  start.setUTCDate(start.getUTCDate() - (days - 1));
  return { from: toIsoDay(start), to: toIsoDay(end) };
}

export function percent(part: number, total: number): string {
  if (total <= 0) return "0%";
  return `${Math.round((part / total) * 100)}%`;
}

export function formatShortDay(isoDay: string): string {
  const [, month, day] = isoDay.split("-");
  return month && day ? `${day}-${month}` : isoDay;
}

export function axisTicks(max: number, count = 4): number[] {
  const step = Math.max(1, Math.ceil(Math.max(max, 1) / count));
  const ticks: number[] = [];
  for (let value = 0; value <= Math.max(max, 1); value += step) ticks.push(value);
  if (ticks[ticks.length - 1] < max) ticks.push(ticks[ticks.length - 1] + step);
  return ticks;
}
