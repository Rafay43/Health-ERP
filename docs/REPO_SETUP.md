# Repository Setup & Hygiene

> Reference notes on repo visibility, `.gitignore` strategy, and secret management for the Health ERP project. Keep in `/docs`.

---

## 1. Repository Visibility

**Decision: Public.**

Rationale:
- The primary goal of this project is an Upwork portfolio piece — a private repo provides no value until it's visible to prospective clients.
- A public repo with visible commit history showing real architectural progression (Clean Architecture setup, module-by-module builds, test coverage growing over time) is itself a selling point — it shows *how* the system was built, not just the end result.
- It's acceptable to go public while incomplete. A WIP badge in the README and a clear roadmap section signal active development rather than an abandoned project.

**Optional staged approach:** keep the repo private for the first 1–2 days while Phase 1 scaffolding and an initial README are in place, then flip to public once it's not an empty shell. Either way — don't wait until "feature complete" to make it public.

**Non-negotiable regardless of visibility:** no secrets ever touch git history, even briefly. A "temporary" committed key stays in history permanently unless the history is rewritten (`git filter-repo` / BFG Repo-Cleaner), which is painful and best avoided entirely by never committing secrets in the first place.

---

## 2. `.gitignore` Strategy

Add `.gitignore` in the **same commit as the first tracked code** — before `bin/`, `obj/`, or `node_modules/` ever get staged. Retrofitting a `.gitignore` after these are already tracked requires an extra `git rm -r --cached` cleanup pass.

**Recommended source:** GitHub's combined templates, or generate one at [gitignore.io](https://www.toptal.com/developers/gitignore) with the `dotnet, visualstudio, node, angular` presets, then extend it as below.

### .NET
```gitignore
bin/
obj/
*.user
*.suo
.vs/
appsettings.Development.json
appsettings.Local.json
*.db
```

### Angular
```gitignore
node_modules/
dist/
.angular/cache/
npm-debug.log*
*.log
```

### Environment / Secrets (critical for a HIPAA-flavored project)
```gitignore
.env
.env.*
*.pfx
*.pem
secrets.json
```

### Docker / Misc
```gitignore
docker-compose.override.yml
.DS_Store
Thumbs.db
```

---

## 3. Secret Management Approach

Document this explicitly in `SECURITY.md` — it reinforces the compliance-aware engineering narrative that's central to this project's positioning:

- **Local development:** ASP.NET Core **User Secrets** (`dotnet user-secrets`) for connection strings and API keys — never `appsettings.json` directly.
- **CI/CD:** GitHub Actions **Encrypted Secrets**, injected as environment variables at build/deploy time.
- **Production:** environment variables sourced from the hosting platform's secret store (Azure App Service Configuration, or a `.env` file outside the repo on a VPS, loaded via Docker Compose `env_file` and never committed).
- **Third-party keys** (Stripe, Twilio, SendGrid, DB connection strings): rotated immediately if ever accidentally exposed, not just removed from the latest commit.

---

## 4. Quick Checklist for First Commit

- [ ] `.gitignore` added before any build artifacts or `node_modules` are staged
- [ ] `appsettings.json` contains no real secrets (placeholders only, e.g., `"ConnectionString": ""`)
- [ ] `README.md` present with at minimum: project description, tech stack, setup instructions
- [ ] `docs/ARCHITECTURE.md`, `docs/SECURITY.md`, `docs/REPO_SETUP.md` (this file) scaffolded, even if partially filled
- [ ] Repository visibility set (public, or staged private → public per above)
- [ ] Branch protection on `main` considered (even solo, a PR-based flow into `main` with CI checks is a good portfolio signal)
