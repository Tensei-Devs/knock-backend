# Gates — BUG-004

## Gates (AGENTS.md §5)
- [ ] `dotnet build Knock.slnx -c Release -warnaserror`: 0 errores y 0 advertencias · `dotnet format --verify-no-changes`
- [ ] Unit (con Architecture), Integration, Security, Database y Contract en verde (resultado real)
- [ ] Cada caso `TC-BUG-004-KK` con su prueba (`python scripts/sdd_lint.py --trace BUG-004`)
- [ ] `codemap.py --check`, `check_exceptions.py`, `sdd_lint.py`, `sync_agents.py --check`, Semgrep
- [ ] `docs/endpoints.md`, `docs/events.md`, `docs/rate-limit-matrix.md`, `docs/openapi.v1.json` al día si el cambio los toca
- [ ] `security-reviewer` LIBERA · `traceability-clerk` sin drift · informe de `story-workflow` en el PR · `Memoria.md` al día
