const SAST_OFFSET_HOURS = 2;

const DAY_IN_MILLISECONDS = 24 * 60 * 60 * 1000;

const SAST_OFFSET_MILLISECONDS =
  SAST_OFFSET_HOURS * 60 * 60 * 1000;

export type SouthAfricanUtcRange = {
  fromUtc?: string;
  toUtc?: string;
};

function parseCalendarDate(value: string): number {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    throw new Error("Invalid calendar date.");
  }

  const [year, month, day] = value.split("-").map(Number);

  const date = new Date(0);
  date.setUTCFullYear(year, month - 1, day);
  date.setUTCHours(0, 0, 0, 0);

  const timestamp = date.getTime();


  if (
    date.getUTCFullYear() !== year ||
    date.getUTCMonth() !== month - 1 ||
    date.getUTCDate() !== day
  ) {
    throw new Error("Invalid calendar date.");
  }

  return timestamp;
}

export function southAfricanDaysToUtcRange(
  from?: string | null,
  to?: string | null,
): SouthAfricanUtcRange {
  const fromDay = from ? parseCalendarDate(from) : undefined;
  const toDay = to ? parseCalendarDate(to) : undefined;

  if (
    fromDay !== undefined &&
    toDay !== undefined &&
    fromDay > toDay
  ) {
    throw new Error("The start date cannot be after the end date.");
  }

  return {
    ...(fromDay !== undefined && {
      fromUtc: new Date(
        fromDay - SAST_OFFSET_MILLISECONDS,
      ).toISOString(),
    }),
    ...(toDay !== undefined && {
      toUtc: new Date(
        toDay + DAY_IN_MILLISECONDS - SAST_OFFSET_MILLISECONDS,
      ).toISOString(),
    }),
  };
}
