import type { HolidayCalendar } from "./common.js";

interface HolidayDay {
  date: string;
  isOffDay: boolean;
}

interface HolidayYear {
  year: number;
  papers: string[];
  days: HolidayDay[];
}

export type FetchText = (url: string) => Promise<string>;

export class HolidayYearNotPublishedError extends Error {}

const HOLIDAY_DATA_URL = "https://raw.githubusercontent.com/NateScarlet/holiday-cn/master";
const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;

function validateDate(value: string, field: string): void {
  const match = DATE_PATTERN.exec(value);
  if (!match) {
    throw new Error(`${field} must be a valid YYYY-MM-DD date: ${value}`);
  }
  const [year, month, day] = value.split("-").map(Number);
  const parsed = new Date(Date.UTC(year, month - 1, day));
  if (parsed.getUTCFullYear() !== year || parsed.getUTCMonth() + 1 !== month || parsed.getUTCDate() !== day) {
    throw new Error(`${field} must be a valid YYYY-MM-DD date: ${value}`);
  }
}

export function parseHolidayYear(expectedYear: number, json: string): HolidayYear {
  const data = JSON.parse(json) as Partial<HolidayYear>;
  if (data.year !== expectedYear) {
    throw new Error(`holiday data year mismatch: expected ${expectedYear}, received ${data.year}`);
  }
  if (!Array.isArray(data.papers) || data.papers.some((paper) => typeof paper !== "string")) {
    throw new Error(`holiday data ${expectedYear} must contain a papers string array`);
  }
  if (!Array.isArray(data.days)) {
    throw new Error(`holiday data ${expectedYear} must contain a days array`);
  }

  for (const [index, day] of data.days.entries()) {
    if (!day || typeof day.date !== "string" || typeof day.isOffDay !== "boolean") {
      throw new Error(`holiday data ${expectedYear} days[${index}] must have date and isOffDay`);
    }
    validateDate(day.date, `holiday data ${expectedYear} days[${index}].date`);
  }

  return data as HolidayYear;
}

export function buildHolidayCalendar(
  years: HolidayYear[],
  coveredFrom: string,
  coveredThrough: string,
): HolidayCalendar {
  validateDate(coveredFrom, "coveredFrom");
  validateDate(coveredThrough, "coveredThrough");
  if (coveredFrom > coveredThrough) throw new Error("coveredFrom must be on or before coveredThrough");
  if (years.length === 0) throw new Error("at least one holiday year is required");

  const excludedDates = new Set<string>();
  for (const year of years) {
    for (const day of year.days) {
      if (day.isOffDay && day.date >= coveredFrom && day.date <= coveredThrough) {
        excludedDates.add(day.date);
      }
    }
  }

  return {
    coveredFrom,
    coveredThrough,
    excludedDates: [...excludedDates].sort(),
  };
}

function currentChinaYear(now: Date): number {
  const parts = new Intl.DateTimeFormat("en-US", {
    timeZone: "Asia/Shanghai",
    year: "numeric",
  }).formatToParts(now);
  const year = Number(parts.find((part) => part.type === "year")?.value);
  if (!Number.isInteger(year)) throw new Error("could not determine current year in Asia/Shanghai");
  return year;
}

export async function fetchHolidayCalendar(
  now = new Date(),
  fetchText: FetchText = async (url) => {
    const response = await fetch(url);
    if (response.status === 404) throw new HolidayYearNotPublishedError(`holiday data is not published: ${url}`);
    if (!response.ok) throw new Error(`holiday data request failed (${response.status}): ${url}`);
    return response.text();
  },
): Promise<HolidayCalendar> {
  const year = currentChinaYear(now);
  const holidayYears = await Promise.all([year - 1, year].map(async (sourceYear) => {
    const url = `${HOLIDAY_DATA_URL}/${sourceYear}.json`;
    return parseHolidayYear(sourceYear, await fetchText(url));
  }));

  const nextYear = year + 1;
  let nextYearData: HolidayYear | null = null;
  try {
    const url = `${HOLIDAY_DATA_URL}/${nextYear}.json`;
    nextYearData = parseHolidayYear(nextYear, await fetchText(url));
    holidayYears.push(nextYearData);
  } catch (error) {
    if (!(error instanceof HolidayYearNotPublishedError)) throw error;
  }

  const currentYearData = holidayYears.find((data) => data.year === year)!;
  if (currentYearData.papers.length === 0 || currentYearData.days.length === 0) {
    throw new Error(`holiday data for current year ${year} is not yet published or is incomplete`);
  }

  const coveredThrough =
    nextYearData !== null && nextYearData.papers.length > 0 && nextYearData.days.length > 0
      ? `${year + 1}-12-31`
      : `${year}-12-31`;

  const coveredFrom = `${year}-01-01`;
  return buildHolidayCalendar(holidayYears, coveredFrom, coveredThrough);
}
