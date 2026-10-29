import {
  createLoader,
  createParser,
  parseAsString,
  parseAsStringEnum,
} from "nuqs/server";

const calendarDatePattern = /^\d{4}-\d{2}-\d{2}$/;

function isValidCalendarDate(value: string): boolean {
  if (!calendarDatePattern.test(value)) {
    return false;
  }

  const [year, month, day] = value.split("-").map(Number);

  const date = new Date(0);
  date.setUTCFullYear(year, month - 1, day);
  date.setUTCHours(0, 0, 0, 0);

  return (
    date.getUTCFullYear() === year &&
    date.getUTCMonth() === month - 1 &&
    date.getUTCDate() === day
  );
}

const parseAsCalendarDate = createParser({
  parse: (value: string) =>
    isValidCalendarDate(value) ? value : null,
  serialize: (value: string) => value,
});

export const contributionFilterParsers = {
  member: parseAsString.withDefault(""),
  from: parseAsCalendarDate.withDefault(""),
  to: parseAsCalendarDate.withDefault(""),
  sort: parseAsStringEnum(["newest", "oldest"]).withDefault("newest"),
};

export const loadContributionFilters = createLoader(
  contributionFilterParsers,
);
