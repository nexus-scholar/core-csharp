# Hardening 13: Search Import Parsers

Status: accepted and implemented locally

## Goal

Make local RIS, Scopus CSV, and BibTeX import parsing preserve ordinary format structure without adding provider or network behavior.

## Invariants

- each RIS `AU` tag represents one author, including comma-form names;
- CSV logical records may span physical lines inside quoted fields;
- escaped CSV quotes and embedded newlines are preserved;
- unterminated quoted CSV rows remain skipped evidence with a deterministic malformed-record warning;
- BibTeX entries use balanced outer delimiters;
- nested braces and multiline BibTeX field values are preserved and normalized deterministically;
- imported raw record text and digests continue to bind the parsed evidence;
- no import path performs deduplication or live retrieval.

## Evidence

- focused tests cover RIS comma authors, quoted multiline CSV, escaped quotes, malformed CSV, and nested/multiline BibTeX;
- a local conformance fixture replays all three realistic forms;
- existing Search import and conformance tests remain green.

## Deferred

- BibTeX macro expansion, string concatenation, and TeX rendering are not claimed;
- malformed-record recovery beyond explicit skipped evidence remains future parser work;
- PHP compatibility remains deferred until generator-backed fixtures and semantic comparison exist.
