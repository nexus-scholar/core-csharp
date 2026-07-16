# FE-08 Native Visual QA

Date: 2026-07-16

Scope: FE-08 slices 1 and 2 Windows-first Avalonia product host.

## Environment

- Windows desktop;
- Release build;
- 1360 x 840 application capture;
- 100% display scaling.

## Observed States

### First Run

- workspace path, workspace id, and title fields were visible and usable;
- the navigation, work area, and effect inspector retained stable columns;
- previewed initialization effects fit without overlap;
- explicit confirm and cancel actions remained distinct.

The first capture exposed invisible text controls because the product host did
not load an Avalonia control theme. The host now loads `FluentTheme`; the
corrected native capture was rechecked before acceptance.

### Loaded Workspace With Import Warnings

- the overview showed one input, two records, two warnings, and zero review
  decisions;
- the `ImportedWithWarnings` state remained visible as attention, not success;
- verify and analysis actions remained available without obscuring import;
- the import form and effect inspector fit the viewport without overlap.

### Failure And Pending Command Safety

- product-host component tests confirm that a failed open clears the previous
  workspace projection and path rather than showing stale data;
- component tests confirm that import remains pending until explicit confirmation
  and that parser warnings render as attention after completion;
- fixed grid dimensions are covered so dynamic labels and command states do not
  resize the workbench columns.

## Result

No blocking layout, overlap, stale-projection, or misleading-state issue remains
for the admitted first-run, loaded, import, warning, and failure states at the
tested Windows viewport. This is focused slice evidence, not an accessibility,
cross-platform, installer, or production-design certification.
