-- Rollback for SeedVocabTermsNaturaleza. Removes only the 20 seeded outdoor
-- vocab terms; any terms added later (different theme, manual entry) are
-- preserved.

DELETE FROM "VocabTerms"
WHERE "Theme" = 'naturaleza'
  AND "Spanish" IN (
      'árbol', 'flor', 'hoja', 'pájaro', 'mariposa',
      'roca', 'sol', 'nube', 'césped', 'ardilla',
      'insecto', 'abeja', 'hormiga', 'luna', 'estrella',
      'arbusto', 'camino', 'charco', 'piña', 'rama'
  );
