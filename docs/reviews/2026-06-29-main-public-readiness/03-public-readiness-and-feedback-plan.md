# Public Readiness And Feedback Plan

## Honest Public Position

Use this public framing:

> Nexus Scholar Core is an audit-grade, local-first C# research workflow kernel. It currently proves strict records, authority boundaries, conformance fixtures, and a sample block-rendering path. It is not yet a finished researcher app.

Do not pitch it as:

- a production systematic-review app;
- an AI paper summarizer;
- a full desktop product;
- a live scholarly-provider tool;
- a PDF extraction/OCR tool;
- a cloud collaboration platform.

## Who To Ask For Feedback First

The best first testers are not general researchers yet. The right first circle is:

1. Developers who care about scientific/research tooling.
2. Systematic-review methodologists who can critique authority and audit boundaries.
3. PhD students or researchers who have experienced messy search/dedup/screening workflows.
4. Open-source maintainers who can review contributor onboarding.
5. One or two UI-minded people who can run the sample host and critique block clarity.

## What To Ask Them To Do

Do not ask: "Can you use Nexus for your review?"

Ask concrete tasks:

1. Read the homepage and tell me what you think the project does.
2. Run the verification commands and report whether setup succeeds.
3. Run the CLI doctor/sample commands and explain what is unclear.
4. Run the Avalonia sample host and inspect the three sample workspaces.
5. Open the Deduplication or Screening module page and identify one unclear boundary.
6. File one feedback issue using a template.

## First Feedback Loop To Build

Minimum public-feedback loop:

- top-level README with current status;
- getting-started tutorial that is not a placeholder;
- issue templates;
- one public "help wanted: first feedback" post;
- pinned GitHub issue explaining what kind of feedback is wanted;
- screenshots or GIFs of the sample host;
- a short "what not to expect yet" section.

## Public Site Review

What is already good:

- static site exists on `gh-pages`;
- homepage has clear positioning;
- blog posts explain motivation and market distinction;
- architecture page is strong;
- module pages exist for current modules;
- internal links pass a local static link check;
- public docs are mostly honest about non-claims.

What blocks first testers:

- getting-started tutorial is a placeholder;
- no issue templates;
- no explicit first-tester path;
- no sample-host screenshot/GIF;
- no "try this in 10 minutes" page;
- no clear distinction between "developer feedback", "methodology feedback", and "researcher workflow feedback";
- no public roadmap page tied to current `origin/main`.

## Repo Landing Page Review

The repo README should be upgraded before inviting testers.

Suggested README structure:

1. One-sentence product claim.
2. What is implemented now.
3. What is explicitly not implemented.
4. Quick start.
5. Try the CLI smoke path.
6. Try the sample host.
7. Where to read architecture.
8. How to give feedback.
9. Branch/state note: `main` is the current public baseline; `gh-pages` is public site.

## Feedback Channels To Add

Add `.github/ISSUE_TEMPLATE/` with:

- `first-tester-run.yml`
- `architecture-boundary-review.yml`
- `research-workflow-use-case.yml`
- `documentation-confusion.yml`
- `bug-report.yml`

Add `.github/PULL_REQUEST_TEMPLATE.md` with:

- behavior changed;
- authority/source of truth;
- tests run;
- non-claims preserved;
- affected docs/ADRs/fixtures;
- public-facing impact.

## What To Show First

Best first public artifact:

> A short public walkthrough: "Run Nexus Scholar Core, inspect a dedup review block, and see why AI suggestions are not decisions."

It should show:

- `dotnet test` green;
- `dotnet run --project src/NexusScholar.Cli -- doctor`;
- `dotnet run --project src/NexusScholar.Cli -- sample`;
- sample host screenshot;
- `samples/block-plans/dedup-review.sample.json`;
- one module page link;
- one issue-template link.
