-- Rollback for SeedWisconsinStandards: removes the 36 seeded WI standards by Code.
-- Codes match the Up script exactly. Safe to re-run.

DELETE FROM "WisconsinStandards"
WHERE "Code" IN (
    -- Standard 1: Interpretive
    'WL.IT.1.a.n1', 'WL.IT.1.a.n2', 'WL.IT.1.a.n3',
    'WL.IT.1.b.n1', 'WL.IT.1.b.n2', 'WL.IT.1.b.n3',
    'WL.IT.1.c.n1', 'WL.IT.1.c.n2', 'WL.IT.1.c.n3',

    -- Standard 2: Interpersonal
    'WL.IP.2.a.n1', 'WL.IP.2.a.n2', 'WL.IP.2.a.n3',
    'WL.IP.2.b.n1', 'WL.IP.2.b.n2', 'WL.IP.2.b.n3',
    'WL.IP.2.c.n1', 'WL.IP.2.c.n2', 'WL.IP.2.c.n3',

    -- Standard 3: Presentational
    'WL.PS.3.a.n1', 'WL.PS.3.a.n2', 'WL.PS.3.a.n3',
    'WL.PS.3.b.n1', 'WL.PS.3.b.n2', 'WL.PS.3.b.n3',
    'WL.PS.3.c.n1', 'WL.PS.3.c.n2', 'WL.PS.3.c.n3',

    -- Standard 4: Intercultural
    'WL.IC.4.a.n+', 'WL.IC.4.b.n+', 'WL.IC.4.c.n+', 'WL.IC.4.d.n+',

    -- Standard 5: Global Competence
    'WL.GCE.5.a.n+', 'WL.GCE.5.b.n+', 'WL.GCE.5.c.n+',
    'WL.GCE.5.d.n+', 'WL.GCE.5.e.n+'
);
