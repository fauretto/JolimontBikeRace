-- One-time repair script for databases restored from the 2016 backup, which lack the
-- constraints declared in create_jolimontbikerace.sql.
-- Run once against the jolimontbikerace database (pgAdmin or psql).

-- Removes duplicate race/category links, keeping the row with the lowest identifier.
DELETE FROM public.race_category a
USING public.race_category b
WHERE a.idrace = b.idrace
  AND a.idcategory = b.idcategory
  AND a.idracecategory > b.idracecategory;

-- Adds the unique constraint declared in create_jolimontbikerace.sql but missing in
-- databases restored from the 2016 backup.
ALTER TABLE public.race_category
    ADD CONSTRAINT race_category_race_category_uq UNIQUE (idrace, idcategory);
