import pytest

import ims_normals as ims


def monthly(stn, year, month, mm, code=0):
    return {"stn_num": stn, "year": year, "month": month, "rain_ttl": mm, "rain_code": code}


def full_rain_year(stn, end_year, per_month):
    """Aug(end_year-1) .. Jul(end_year): the IMS rain year labelled by the year it ends."""
    return [monthly(stn, end_year - 1 if m >= 8 else end_year, m, per_month) for m in (8, 9, 10, 11, 12, 1, 2, 3, 4, 5, 6, 7)]


def test_rain_year_is_august_to_july_labelled_by_ending_year():
    recs = [monthly(1, 1990, 11, 30.0), monthly(1, 1991, 2, 40.0), monthly(1, 1991, 8, 99.0)]
    totals = ims.monthly_to_rain_years(recs, required=(11, 2))
    assert totals[1][1991] == 70.0           # Nov 1990 + Feb 1991
    assert 1992 not in totals[1]             # Aug 1991 opens the next (incomplete) rain year


def test_incomplete_or_doubtful_years_are_dropped():
    recs = full_rain_year(1, 1990, 10.0)
    recs = [r for r in recs if r["month"] != 1]                                     # January missing
    recs += [dict(r, rain_code=5) if r["month"] == 3 else r for r in full_rain_year(1, 1991, 10.0)]  # March doubtful
    recs += full_rain_year(1, 1992, 10.0)
    totals = ims.monthly_to_rain_years(recs)
    assert set(totals[1]) == {1992} and totals[1][1992] == 120.0


def test_normals_merge_colocated_gauges_and_require_min_years():
    stations = {1: {"stn_num": 1, "stn_name": "A MAN", "stn_long": 35.0, "stn_lat": 31.0},
                2: {"stn_num": 2, "stn_name": "A", "stn_long": 35.005, "stn_lat": 31.0},   # ~0.5 km: same site
                3: {"stn_num": 3, "stn_name": "B", "stn_long": 35.2, "stn_lat": 31.0}}
    annual = {1: {y: 100.0 for y in range(1981, 1995)},                 # 14 years
              2: {y: 200.0 for y in range(1990, 2011)},                 # overlaps 1990-94; adds 1995-2010
              3: {y: 50.0 for y in range(1981, 1990)}}                  # 9 years only -> dropped
    sites = ims.normals(annual, stations, first=1981, last=2010, min_years=20, merge_km=1.0)
    assert len(sites) == 1
    s = sites[0]
    assert s["ids"] == [1, 2] and s["years"] == 30 and s["name"] == "A MAN"
    assert s["mm"] == pytest.approx((14 * 100 + 16 * 200) / 30, abs=0.05)  # first gauge wins where both report
