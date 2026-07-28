-- ============================================================================
-- create_jolimontbikerace.sql
--
-- Purpose : Creates a fresh jolimontbikerace database -- schema
--           reverse-engineered from the 2016 pg_dump backup
--           (joilomntbikerace2016official.backup).
-- Date    : 2026-07-22
--
-- Note    : Foreign keys were absent in the original dump and are now made
--           explicit below, based on the observed column naming and usage
--           patterns (idbiker, idrace, idcategory references).
-- ============================================================================

-- ======================================================================
-- Database creation
-- ======================================================================

-- Safety option: uncomment the line below to drop an existing database
-- before recreating it. Left commented out by default to avoid accidental
-- data loss.
-- DROP DATABASE IF EXISTS jolimontbikerace;

CREATE DATABASE jolimontbikerace WITH TEMPLATE = template0 ENCODING = 'UTF8';

connect jolimontbikerace

SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;

BEGIN;

-- ======================================================================
-- Table: biker
-- Rider registry -- one row per registered biker.
-- ======================================================================

-- BIGSERIAL replaces the original explicit sequence biker_idbiker_seq.
CREATE TABLE public.biker (
    idbiker    BIGSERIAL,
    firstname  text,
    lastname   text,
    yearofbirth integer,
    address    text,
    email      text,
    telephone  text,
    natel      text,
    CONSTRAINT biker_pkey PRIMARY KEY (idbiker)
);

COMMENT ON TABLE public.biker IS 'Rider registry: personal and contact details for each registered biker.';

-- ======================================================================
-- Table: race
-- Race event -- one row per race (e.g. Adultes, Enfants).
-- ======================================================================

-- BIGSERIAL replaces the original explicit sequence race_idrace_seq.
CREATE TABLE public.race (
    idrace     BIGSERIAL,
    racename   text NOT NULL,
    racetick   bigint DEFAULT 0,
    CONSTRAINT race_pkey PRIMARY KEY (idrace)
);

COMMENT ON TABLE public.race IS 'Race event: a named race (e.g. Adultes, Enfants). racetick is the race start time, stored as .NET DateTime.Ticks.';

-- ======================================================================
-- Table: category
-- Category with bib range -- e.g. age/gender categories with a
-- reserved range of bib (race) numbers.
-- ======================================================================

-- BIGSERIAL replaces the original explicit sequence category_idcategory_seq.
CREATE TABLE public.category (
    idcategory   BIGSERIAL,
    categoryname text NOT NULL,
    minnumber    integer DEFAULT 0,
    maxnumber    integer DEFAULT 0,
    CONSTRAINT category_pkey PRIMARY KEY (idcategory)
);

COMMENT ON TABLE public.category IS 'Category with bib range: a named category (e.g. age/gender group) with its allowed bib-number range (minnumber-maxnumber).';

-- ======================================================================
-- Table: race_category
-- Race <-> category link -- which categories are available in which race.
-- ======================================================================

-- BIGSERIAL replaces the original explicit sequence race_category_idracecategory_seq.
CREATE TABLE public.race_category (
    idracecategory BIGSERIAL,
    idrace         bigint NOT NULL,
    idcategory     bigint NOT NULL,
    CONSTRAINT race_category_pkey PRIMARY KEY (idracecategory),
    CONSTRAINT race_category_idrace_fkey
        FOREIGN KEY (idrace) REFERENCES public.race (idrace) ON DELETE CASCADE,
    CONSTRAINT race_category_idcategory_fkey
        FOREIGN KEY (idcategory) REFERENCES public.category (idcategory) ON DELETE CASCADE,
    CONSTRAINT race_category_race_category_uq UNIQUE (idrace, idcategory)
);

COMMENT ON TABLE public.race_category IS 'Race<->category link: associates a category with a race it is offered in.';

-- ======================================================================
-- Table: biker_race_category
-- Registration with bib number -- a biker's entry into a race, in a
-- category, with an assigned bib (race) number.
-- ======================================================================

-- BIGSERIAL replaces the original explicit sequence
-- biker_race_category_idbikeracecategory_seq.
CREATE TABLE public.biker_race_category (
    idbikeracecategory BIGSERIAL,
    idbiker            bigint NOT NULL,
    idrace             bigint NOT NULL,
    idcategory         bigint,
    racenumber         integer,
    -- Composite primary key kept exactly as in the original dump.
    CONSTRAINT biker_race_category_pkey PRIMARY KEY (idbikeracecategory, idbiker, idrace),
    CONSTRAINT biker_race_category_idbiker_fkey
        FOREIGN KEY (idbiker) REFERENCES public.biker (idbiker) ON DELETE CASCADE,
    CONSTRAINT biker_race_category_idrace_fkey
        FOREIGN KEY (idrace) REFERENCES public.race (idrace) ON DELETE CASCADE,
    CONSTRAINT biker_race_category_idcategory_fkey
        FOREIGN KEY (idcategory) REFERENCES public.category (idcategory) ON DELETE SET NULL,
    CONSTRAINT biker_race_category_racenumber_uq UNIQUE (idrace, racenumber)
);

COMMENT ON TABLE public.biker_race_category IS 'Registration with bib number: links a biker to a race/category and assigns their racenumber (bib).';

-- ======================================================================
-- Table: race_standings
-- Raw finish-line crossing log -- every timing tick recorded for a
-- biker in a race, in the order it was captured.
-- ======================================================================

-- BIGSERIAL replaces the original explicit sequence race_standings_idstanding_seq.
CREATE TABLE public.race_standings (
    idstanding BIGSERIAL,
    idbiker    bigint NOT NULL,
    idrace     bigint NOT NULL,
    tickindex  bigint NOT NULL,
    tick       bigint NOT NULL,
    CONSTRAINT race_standings_pkey PRIMARY KEY (idstanding),
    CONSTRAINT race_standings_idbiker_fkey
        FOREIGN KEY (idbiker) REFERENCES public.biker (idbiker) ON DELETE CASCADE,
    CONSTRAINT race_standings_idrace_fkey
        FOREIGN KEY (idrace) REFERENCES public.race (idrace) ON DELETE CASCADE
);

COMMENT ON TABLE public.race_standings IS 'Raw finish-line crossing log: every timing tick captured for a biker in a race (tickindex = capture order). tick is .NET DateTime.Ticks.';

-- ======================================================================
-- Table: standing
-- Computed final classification -- the resolved race position and
-- time for each biker in a race.
-- ======================================================================

-- BIGSERIAL replaces the original explicit sequence standing_idstanding_seq.
CREATE TABLE public.standing (
    idstanding   BIGSERIAL,
    idbiker      bigint NOT NULL,
    idrace       bigint NOT NULL,
    raceposition integer NOT NULL,
    tick         bigint NOT NULL,
    racetime     text,
    gap          text,
    CONSTRAINT standing_pkey PRIMARY KEY (idstanding),
    CONSTRAINT standing_idbiker_fkey
        FOREIGN KEY (idbiker) REFERENCES public.biker (idbiker) ON DELETE CASCADE,
    CONSTRAINT standing_idrace_fkey
        FOREIGN KEY (idrace) REFERENCES public.race (idrace) ON DELETE CASCADE
);

COMMENT ON TABLE public.standing IS 'Computed final classification: resolved race position, time and gap for a biker in a race. tick is .NET DateTime.Ticks.';

-- ======================================================================
-- Indexes on FK columns not already covered by a PK/unique constraint
-- ======================================================================

CREATE INDEX ix_race_standings_idrace ON public.race_standings (idrace);
CREATE INDEX ix_race_standings_idbiker ON public.race_standings (idbiker);

CREATE INDEX ix_standing_idrace ON public.standing (idrace);
CREATE INDEX ix_standing_idbiker ON public.standing (idbiker);

CREATE INDEX ix_biker_race_category_idrace ON public.biker_race_category (idrace);
CREATE INDEX ix_biker_race_category_idbiker ON public.biker_race_category (idbiker);

COMMIT;
