-- Repair helper for databases restored from the 2016 backup, which lack the
-- constraints declared in create_jolimontbikerace.sql.
--
-- Step 1: report the registrations that share the same bib number within the same race.
-- This script intentionally does NOT modify data: the duplicated registrations belong to
-- historical results, so a person must decide which rider keeps the bib number and fix
-- the registrations manually (in the application or directly in the database).

SELECT brc.idbikeracecategory, brc.idrace, r.racename, brc.racenumber,
       brc.idbiker, b.firstname, b.lastname
FROM public.biker_race_category brc
JOIN public.race r ON r.idrace = brc.idrace
JOIN public.biker b ON b.idbiker = brc.idbiker
WHERE brc.racenumber IS NOT NULL
  AND (brc.idrace, brc.racenumber) IN (
      SELECT idrace, racenumber
      FROM public.biker_race_category
      WHERE racenumber IS NOT NULL
      GROUP BY idrace, racenumber
      HAVING COUNT(*) > 1)
ORDER BY brc.idrace, brc.racenumber, brc.idbikeracecategory;

-- Step 2: once the report above returns no rows, add the unique constraint declared in
-- create_jolimontbikerace.sql but missing in databases restored from the 2016 backup.
-- This statement fails as long as duplicated bib numbers remain.

ALTER TABLE public.biker_race_category
    ADD CONSTRAINT biker_race_category_racenumber_uq UNIQUE (idrace, racenumber);
