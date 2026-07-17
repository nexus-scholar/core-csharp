# Nexus Scholar Core Astro Site

This directory contains the Astro source for the Nexus Scholar Core GitHub
Pages site.

## Structure

- `src/pages/` - route-preserving Astro pages.
- `public/assets/` - shared styles, scripts, and generated visuals copied
  unchanged into the build.
- `public/sitemap.xml` and `public/robots.txt` - current discovery metadata.
- `astro.config.mjs` - static output and canonical-site configuration.
- `dist/` - generated deployment artifact; never committed.

The site remains completely static. Astro is a build-time dependency only; no
Node.js server or browser framework runtime is deployed.

## Publish Source

Repository Pages build type is GitHub Actions. Pull requests run Astro source
checking and a production build. Pushes to `main` upload `site/dist/` and deploy
that immutable artifact. The historical `gh-pages` branch is retained for
provenance but is no longer the deploy source.

## Local Commands

Run these commands from this directory:

```powershell
npm ci
npm run check
npm run build
npm run preview
```

The committed `package-lock.json` is the dependency authority for local and CI
builds.

## Migration Boundary

The first Astro slice preserves the existing routes, content, visual design,
and static assets while moving page sources under `src/pages/`. Shared layouts
and content collections can be extracted in later reviewable slices without
combining a framework migration with a public redesign.

## Custom Domain

The canonical public hostname is `nexus.mouadh.org`. Because this repository
publishes through a custom GitHub Actions workflow, GitHub stores the custom
domain in repository settings and ignores a checked-in `CNAME` file. Do not add
one to this directory. The domain is verified for the `nexus-scholar-org`
GitHub organization; configuration and verification commands are documented in
`docs/ops/GITHUB-PAGES-CUSTOM-DOMAIN.md`.
