import assert from "node:assert/strict";
import test from "node:test";
import { buildHolidayCalendar, fetchHolidayCalendar, HolidayYearNotPublishedError, parseHolidayYear } from "../src/fetch-holidays.js";

function holidayYear(year: number, days: { date: string; isOffDay: boolean }[] = []) {
  return JSON.stringify({
    year,
    papers: [`https://www.gov.cn/holiday-${year}`],
    days: days.map((day) => ({ name: "holiday", ...day })),
  });
}

test("parseHolidayYear validates the year and daily fields", () => {
  const parsed = parseHolidayYear(2026, holidayYear(2026, [{ date: "2026-10-01", isOffDay: true }]));
  assert.equal(parsed.days[0].date, "2026-10-01");
  assert.throws(() => parseHolidayYear(2025, holidayYear(2026)), /year mismatch/);
  assert.throws(() => parseHolidayYear(2026, holidayYear(2026, [{ date: "2026-02-31", isOffDay: true }])), /valid YYYY-MM-DD/);
  assert.throws(() => parseHolidayYear(2026, JSON.stringify({ year: 2026, papers: [], days: [{ date: "2026-10-01", isOffDay: "yes" }] })), /isOffDay/);
});

test("buildHolidayCalendar keeps only in-range rest days and sorts/deduplicates", () => {
  const source = parseHolidayYear(2026, holidayYear(2026, [
    { date: "2026-10-02", isOffDay: true },
    { date: "2026-09-30", isOffDay: true },
    { date: "2026-10-01", isOffDay: false },
  ]));
  const calendar = buildHolidayCalendar([source], "2026-10-01", "2026-10-31");
  assert.deepEqual(calendar, {
    coveredFrom: "2026-10-01",
    coveredThrough: "2026-10-31",
    excludedDates: ["2026-10-02"],
  });
  assert.throws(() => buildHolidayCalendar([source], "2026-11-01", "2026-10-31"), /on or before/);
});

test("fetchHolidayCalendar requests adjacent source years and emits two-year coverage", async () => {
  const requested: string[] = [];
  const calendar = await fetchHolidayCalendar(new Date("2026-09-27T00:00:00Z"), async (url) => {
    requested.push(url);
    const year = Number(url.match(/\/(\d{4})\.json$/)?.[1]);
    return holidayYear(year, [{ date: `${year}-10-01`, isOffDay: true }]);
  });

  assert.deepEqual(requested.map((url) => Number(url.match(/\/(\d{4})\.json$/)?.[1])), [2025, 2026, 2027]);
  assert.equal(calendar.coveredFrom, "2026-01-01");
  assert.equal(calendar.coveredThrough, "2027-12-31");
  assert.deepEqual(calendar.excludedDates, ["2026-10-01", "2027-10-01"]);
});

test("fetchHolidayCalendar does not claim coverage for an unpublished next year", async () => {
  const calendar = await fetchHolidayCalendar(new Date("2026-09-27T00:00:00Z"), async (url) => {
    const year = Number(url.match(/\/(\d{4})\.json$/)?.[1]);
    return holidayYear(year, year === 2027 ? [] : [{ date: `${year}-10-01`, isOffDay: true }]);
  });

  assert.equal(calendar.coveredFrom, "2026-01-01");
  assert.equal(calendar.coveredThrough, "2026-12-31");
  assert.deepEqual(calendar.excludedDates, ["2026-10-01"]);
});

test("fetchHolidayCalendar uses current-year coverage when the next file is absent", async () => {
  const calendar = await fetchHolidayCalendar(new Date("2026-09-27T00:00:00Z"), async (url) => {
    const year = Number(url.match(/\/(\d{4})\.json$/)?.[1]);
    if (year === 2027) throw new HolidayYearNotPublishedError("not published");
    return holidayYear(year, [{ date: `${year}-10-01`, isOffDay: true }]);
  });

  assert.equal(calendar.coveredThrough, "2026-12-31");
});
