# Nexus Scholar Core Pages

This directory is the GitHub Pages source for the Nexus Scholar Core project site.

## Structure

- `index.html` - project homepage and ecosystem entry point.
- `about/` - project vision and principles.
- `blog/` - public project narrative, motivation, positioning, and community posts.
- `developers/` - developer documentation.
- `tutorials/` - tutorial pages.
- `assets/` - shared styles, scripts, and generated visuals.

The site is intentionally static and dependency-free. The `pages` workflow deploys it from `main` with the GitHub Pages artifact pipeline.

## Publish Source

Repository Pages build type is GitHub Actions. The historical `gh-pages` branch is retained for provenance but is no longer the deploy source.
