# Migrations

EF Core handles **schema** migrations (auto-generated from entity changes).
**Data** migrations use a SQL-file-with-rollback pattern via [`BaseMigration`](BaseMigration.cs).

Pattern adopted from Mark's Allevo Therapeutics repo. See the original at
`E:\Documents\Work\allevotherapeutics\src\Allevo.Infrastructure\Persistence\Migrations\README.md`.

## Schema migration (the EF default)

When you change a domain entity:

```pwsh
dotnet ef migrations add SomeSchemaChange `
  --project src/LakeCountrySpanish.Web `
  --output-dir Migrations
```

EF generates a `.cs` file with `migrationBuilder.AddColumn(...)` etc. Leave it alone.

## Data migration (the SQL pattern)

For seed data, content, bulk fixes, or anything where SQL reads better than C#:

1. Create the SQL files first:
   - `Migrations/Scripts/SeedSomething.sql` — the Up
   - `Migrations/Scripts/SeedSomething.rollback.sql` — the Down

2. Generate the migration shell:
   ```pwsh
   dotnet ef migrations add SeedSomething `
     --project src/LakeCountrySpanish.Web `
     --output-dir Migrations
   ```

3. Replace the generated `.cs` body with the `BaseMigration` pattern:

   ```csharp
   public partial class SeedSomething : BaseMigration
   {
       protected override void Up(MigrationBuilder mb) => RunSql(mb);
       protected override void Down(MigrationBuilder mb) => RunSqlRollback(mb);
   }
   ```

4. Apply:
   ```pwsh
   dotnet ef database update `
     --project src/LakeCountrySpanish.Web
   ```

`MigrationHelper` finds the script by class name at runtime. The `.csproj`
copies `Scripts/*.sql` to the output folder so it works in published builds too.

## Conventions

- **Idempotent Ups** — use `IF NOT EXISTS` / `ON CONFLICT DO NOTHING` so re-running
  is safe. Migrations only re-apply if rolled back, but the safety helps in dev
  when re-creating local DBs.
- **Rollback is real** — `Down` should actually undo `Up`. Don't write
  `-- nothing to undo` placeholders.
- **No password hashes in SQL** — Identity user creation must go through
  `UserManager` (see `SeedData.InitializeAsync`). PBKDF2 hashing is fragile to
  reproduce by hand.
- **Schema changes belong in EF** — don't `CREATE TABLE` in raw SQL or
  the EF model snapshot drifts and future migrations collide.
