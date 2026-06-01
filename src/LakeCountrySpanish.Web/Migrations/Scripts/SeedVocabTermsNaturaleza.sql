-- Seed initial outdoor / nature vocabulary used by the first scavenger-hunt
-- worksheet. MediaAssetId is left NULL — images get generated post-seed by
-- the admin "generate missing vocab images" action via Replicate.
--
-- INSERTs use ON CONFLICT against the unique (Theme, Spanish) index so this
-- script is safe to re-run.

INSERT INTO "VocabTerms" ("Spanish", "Article", "English", "Theme", "MediaAssetId", "CreatedAt") VALUES
    ('árbol',    'el', 'tree',      'naturaleza', NULL, NOW() AT TIME ZONE 'UTC'),
    ('flor',     'la', 'flower',    'naturaleza', NULL, NOW() AT TIME ZONE 'UTC'),
    ('hoja',     'la', 'leaf',      'naturaleza', NULL, NOW() AT TIME ZONE 'UTC'),
    ('pájaro',   'el', 'bird',      'naturaleza', NULL, NOW() AT TIME ZONE 'UTC'),
    ('mariposa', 'la', 'butterfly', 'naturaleza', NULL, NOW() AT TIME ZONE 'UTC'),
    ('roca',     'la', 'rock',      'naturaleza', NULL, NOW() AT TIME ZONE 'UTC'),
    ('sol',      'el', 'sun',       'naturaleza', NULL, NOW() AT TIME ZONE 'UTC'),
    ('nube',     'la', 'cloud',     'naturaleza', NULL, NOW() AT TIME ZONE 'UTC'),
    ('césped',   'el', 'grass',     'naturaleza', NULL, NOW() AT TIME ZONE 'UTC'),
    ('ardilla',  'la', 'squirrel',  'naturaleza', NULL, NOW() AT TIME ZONE 'UTC'),
    ('insecto',  'el', 'insect',    'naturaleza', NULL, NOW() AT TIME ZONE 'UTC'),
    ('abeja',    'la', 'bee',       'naturaleza', NULL, NOW() AT TIME ZONE 'UTC'),
    ('hormiga',  'la', 'ant',       'naturaleza', NULL, NOW() AT TIME ZONE 'UTC'),
    ('luna',     'la', 'moon',      'naturaleza', NULL, NOW() AT TIME ZONE 'UTC'),
    ('estrella', 'la', 'star',      'naturaleza', NULL, NOW() AT TIME ZONE 'UTC'),
    ('arbusto',  'el', 'bush',      'naturaleza', NULL, NOW() AT TIME ZONE 'UTC'),
    ('camino',   'el', 'path',      'naturaleza', NULL, NOW() AT TIME ZONE 'UTC'),
    ('charco',   'el', 'puddle',    'naturaleza', NULL, NOW() AT TIME ZONE 'UTC'),
    ('piña',     'la', 'pinecone',  'naturaleza', NULL, NOW() AT TIME ZONE 'UTC'),
    ('rama',     'la', 'branch',    'naturaleza', NULL, NOW() AT TIME ZONE 'UTC')
ON CONFLICT ("Theme", "Spanish") DO NOTHING;
