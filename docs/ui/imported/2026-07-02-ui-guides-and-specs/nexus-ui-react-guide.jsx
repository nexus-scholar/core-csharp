import React, { useMemo, useState } from "react";
import "./nexus-ui-react-guide.css";

const workflowSteps = [
  ["Workspace", "done", "Project folder created"],
  ["Import", "done", "4 local exports"],
  ["Verify", "done", "Files unchanged"],
  ["Analyze", "done", "Workspace plan exists"],
  ["Review", "current", "Human review needed"],
];

const records = [
  {
    id: "rec-001",
    title: "Rayyan — a web and mobile app for systematic reviews",
    creators: "Ouzzani; Hammady; Fedorowicz",
    year: "2016",
    venue: "Systematic Reviews",
    source: "Scopus",
    identifier: "10.1186/s13643-016-0384-4",
    warnings: 0,
    duplicateState: "Review required",
    duplicateTone: "orange",
    importId: "search-001",
    traceId: "search-001.import-trace",
    sourceDigest: "sha256:9c0f...",
    rawDigest: "sha256:bb32...",
  },
  {
    id: "rec-002",
    title: "Rayyan: a web and mobile application for systematic reviews",
    creators: "Ouzzani et al.",
    year: "2016",
    venue: "Systematic Reviews",
    source: "Web of Science",
    identifier: "missing",
    warnings: 1,
    duplicateState: "Review required",
    duplicateTone: "orange",
    importId: "search-002",
    traceId: "search-002.import-trace",
    sourceDigest: "sha256:71aa...",
    rawDigest: "sha256:90ef...",
  },
  {
    id: "rec-003",
    title: "ASReview: Active learning for systematic reviews",
    creators: "van de Schoot et al.",
    year: "2021",
    venue: "Nature Machine Intelligence",
    source: "Scopus",
    identifier: "10.5281/zenodo.3345592",
    warnings: 0,
    duplicateState: "Exact cluster",
    duplicateTone: "green",
    importId: "search-001",
    traceId: "search-001.import-trace",
    sourceDigest: "sha256:9c0f...",
    rawDigest: "sha256:18cd...",
  },
  {
    id: "rec-004",
    title: "Machine learning assisted title and abstract screening",
    creators: "Marshall; Wallace",
    year: "2019",
    venue: "Review Methods",
    source: "OpenAlex",
    identifier: "missing",
    warnings: 2,
    duplicateState: "No duplicate evidence",
    duplicateTone: "gray",
    importId: "search-003",
    traceId: "search-003.import-trace",
    sourceDigest: "sha256:88e2...",
    rawDigest: "sha256:20ba...",
  },
];

function Pill({ tone = "gray", children }) {
  return <span className={`nx-pill ${tone}`}>{children}</span>;
}

function Button({ children, variant = "", onClick }) {
  return <button className={`nx-button ${variant}`} onClick={onClick}>{children}</button>;
}

function AppShell() {
  const [screen, setScreen] = useState("overview");
  const nav = [
    ["Welcome", "welcome"],
    ["New Workspace", "newWorkspace"],
    ["Overview", "overview"],
    ["Imports", "imports"],
    ["Evidence Records", "records"],
    ["Verification", "verification"],
    ["Analysis", "analysis"],
    ["Review Queue", "review"],
    ["Duplicate Clusters", "clusters"],
    ["Duplicate Detail", "duplicateDetail"],
  ];
  return (
    <div className="nexus-app">
      <aside className="nx-sidebar">
        <div className="nx-brand">Nexus Scholar<br /><span className="nx-muted">Research evidence workspace</span></div>
        <div className="nx-nav-title">Screens</div>
        {nav.map(([label, key]) => (
          <button key={key} className={`nx-nav ${screen === key ? "active" : ""}`} onClick={() => setScreen(key)}>{label}</button>
        ))}
      </aside>
      <main>
        <TopBar />
        <div className="nx-content">
          {screen === "welcome" && <WelcomeScreen onNew={() => setScreen("newWorkspace")} />}
          {screen === "newWorkspace" && <NewWorkspaceScreen />}
          {screen === "overview" && <ProjectOverviewScreen onReview={() => setScreen("review")} />}
          {screen === "imports" && <ImportSearchExportsScreen onVerify={() => setScreen("verification")} />}
          {screen === "records" && <EvidenceRecordsScreen onCompare={() => setScreen("duplicateDetail")} />}
          {screen === "verification" && <VerificationScreen onAnalyze={() => setScreen("analysis")} />}
          {screen === "analysis" && <AnalysisScreen onReview={() => setScreen("review")} />}
          {screen === "review" && <ReviewQueueScreen onCompare={() => setScreen("duplicateDetail")} />}
          {screen === "clusters" && <DuplicateClustersScreen onCompare={() => setScreen("duplicateDetail")} />}
          {screen === "duplicateDetail" && <DuplicateDetailScreen />}
        </div>
      </main>
    </div>
  );
}

function TopBar() {
  return (
    <header className="nx-topbar">
      <div>
        <strong>AI screening tools review</strong>
        <div style={{ display: "flex", gap: 8, marginTop: 4 }}>
          <Pill tone="orange">Review ready</Pill>
          <Pill tone="blue">Local-only</Pill>
          <Pill>No providers</Pill>
          <Pill>Decisions locked</Pill>
        </div>
      </div>
      <div style={{ display: "flex", gap: 8 }}>
        <Button variant="ghost">CLI equivalent</Button>
        <Button variant="primary">Open Review Queue</Button>
      </div>
    </header>
  );
}

function WorkflowStepper() {
  return (
    <div className="nx-grid nx-stepper">
      {workflowSteps.map(([label, state, detail]) => (
        <div key={label} className="nx-step">
          <Pill tone={state === "done" ? "green" : state === "current" ? "orange" : "gray"}>{state}</Pill>
          <h3>{label}</h3>
          <p className="nx-muted">{detail}</p>
        </div>
      ))}
    </div>
  );
}

function SummaryCards() {
  const cards = [
    ["Search exports", "4"],
    ["Imported records", "428"],
    ["Parser warnings", "5"],
    ["Exact clusters", "31"],
    ["Review candidates", "18"],
  ];
  return (
    <div className="nx-grid nx-summary">
      {cards.map(([label, value]) => <div className="nx-summary-card" key={label}><div className="nx-muted">{label}</div><h1>{value}</h1></div>)}
    </div>
  );
}

function WelcomeScreen({ onNew }) {
  return (
    <div className="nx-grid nx-two">
      <section className="nx-panel">
        <h1>Welcome to Nexus Scholar</h1>
        <p className="nx-muted">Recent local research workspaces.</p>
        {[
          "AI screening tools review · Review ready · 18 candidates need review",
          "Tomato disease segmentation review · Imported · verify next",
          "Medical imaging synthesis review · Analyzed",
        ].map(item => <div className="nx-list-item" key={item}>{item}</div>)}
      </section>
      <section className="nx-panel">
        <h2>Actions</h2>
        <div className="nx-grid">
          <Button variant="primary" onClick={onNew}>New Research Workspace</Button>
          <Button>Open Existing Workspace</Button>
          <Button>Run Local Demo</Button>
        </div>
      </section>
    </div>
  );
}

function NewWorkspaceScreen() {
  return (
    <section className="nx-panel">
      <h1>New Research Workspace</h1>
      <p className="nx-muted">Create the local folder structure used by nexus init.</p>
      <div className="nx-grid nx-two">
        <div className="nx-filter">
          <div className="nx-filter-row active"><span>Evidence Review</span><span>v0</span></div>
          <div className="nx-filter-row"><span>Systematic Review</span><span>later</span></div>
          <div className="nx-filter-row"><span>Scoping Review</span><span>later</span></div>
          <div className="nx-filter-row"><span>Dataset Audit</span><span>later</span></div>
        </div>
        <div className="nx-grid">
          <label>Workspace name<input defaultValue="AI screening tools review" /></label>
          <label>Location<input defaultValue="C:/Users/researcher/NexusWorkspaces/AI-screening-tools-review" /></label>
          <Pill tone="blue">Local folder only · no uploads · no providers</Pill>
          <Button variant="primary">Create Workspace</Button>
        </div>
      </div>
    </section>
  );
}

function ProjectOverviewScreen({ onReview }) {
  return (
    <section className="nx-panel">
      <h1>Project Overview</h1>
      <p className="nx-muted">Compass screen: state, workflow, counts, and next action.</p>
      <WorkflowStepper />
      <SummaryCards />
      <div className="nx-card" style={{ marginTop: 14 }}>
        <h2>Recommended next action</h2>
        <p className="nx-muted">18 duplicate candidates need human review. Decision execution remains locked.</p>
        <Button variant="primary" onClick={onReview}>Open Review Queue</Button>
      </div>
    </section>
  );
}

function ImportSearchExportsScreen({ onVerify }) {
  return (
    <section className="nx-panel">
      <h1>Import Search Exports</h1>
      <p className="nx-muted">Drag-and-drop local CSV, RIS, or BibTeX exports.</p>
      <div className="nx-card">Drop files here or choose files. Supported now: CSV, RIS, BibTeX.</div>
      <div className="nx-table-card" style={{ marginTop: 14 }}>
        <table className="nx-table"><thead><tr><th>File</th><th>Source</th><th>Format</th><th>Query ID</th><th>Status</th></tr></thead><tbody>
          <tr><td>scopus.csv</td><td>Scopus</td><td>CSV</td><td className="nx-mono">search-001</td><td><Pill tone="yellow">Imported with warnings</Pill></td></tr>
          <tr><td>wos.ris</td><td>Web of Science</td><td>RIS</td><td className="nx-mono">search-002</td><td><Pill tone="green">Imported</Pill></td></tr>
        </tbody></table>
      </div>
      <Button variant="primary" onClick={onVerify}>Verify Workspace</Button>
    </section>
  );
}

function EvidenceRecordsScreen({ onCompare }) {
  return <EvidenceRecordsTable records={records} onCompare={onCompare} />;
}

function EvidenceRecordsTable({ records, onCompare }) {
  const [selectedId, setSelectedId] = useState(records[0].id);
  const selected = useMemo(() => records.find(r => r.id === selectedId) || records[0], [records, selectedId]);

  return (
    <section>
      <h1>Evidence Records</h1>
      <p className="nx-muted">Zotero-style record browser: filters, DataGrid, and read-only inspector.</p>
      <div className="nx-grid nx-three">
        <aside className="nx-filter">
          <h3>Library</h3>
          <div className="nx-filter-row active"><span>All records</span><span>428</span></div>
          <div className="nx-filter-row"><span>Needs duplicate review</span><span>18</span></div>
          <div className="nx-filter-row"><span>Exact clusters</span><span>31</span></div>
          <div className="nx-filter-row"><span>Parser warnings</span><span>5</span></div>
          <h3>Sources</h3>
          <div className="nx-filter-row"><span>Scopus</span><span>120</span></div>
          <div className="nx-filter-row"><span>Web of Science</span><span>96</span></div>
          <div className="nx-filter-row"><span>OpenAlex</span><span>182</span></div>
        </aside>
        <div className="nx-table-card">
          <div className="nx-table-toolbar"><input defaultValue="Rayyan OR ASReview" /><Button variant="ghost">Columns</Button></div>
          <div className="nx-table-scroll">
            <table className="nx-table"><thead><tr><th>Title</th><th>Creators</th><th>Year</th><th>Venue</th><th>Source</th><th>Identifier</th><th>Warnings</th><th>Duplicate state</th></tr></thead><tbody>
              {records.map(record => (
                <tr key={record.id} className={record.id === selectedId ? "selected" : ""} onClick={() => setSelectedId(record.id)}>
                  <td>{record.title}</td><td>{record.creators}</td><td>{record.year}</td><td>{record.venue}</td><td>{record.source}</td><td>{record.identifier}</td><td>{record.warnings}</td><td><Pill tone={record.duplicateTone}>{record.duplicateState}</Pill></td>
                </tr>
              ))}
            </tbody></table>
          </div>
        </div>
        <aside className="nx-inspector">
          <Pill tone={selected.duplicateTone}>{selected.duplicateState}</Pill>
          <h2>{selected.title}</h2>
          <dl>
            <dt>Source</dt><dd>{selected.source}</dd>
            <dt>Identifier</dt><dd>{selected.identifier}</dd>
            <dt>Import ID</dt><dd className="nx-mono">{selected.importId}</dd>
            <dt>Trace</dt><dd className="nx-mono">{selected.traceId}</dd>
            <dt>Source digest</dt><dd className="nx-mono">{selected.sourceDigest}</dd>
            <dt>Raw digest</dt><dd className="nx-mono">{selected.rawDigest}</dd>
          </dl>
          <Button variant="primary" onClick={onCompare}>Open comparison</Button>
        </aside>
      </div>
    </section>
  );
}

function VerificationScreen({ onAnalyze }) {
  return (
    <section className="nx-panel">
      <h1>Workspace Verification <Pill tone="green">Valid</Pill></h1>
      <div className="nx-grid nx-two">
        <div className="nx-card"><h2>File integrity</h2><p>✓ 4 files unchanged<br />✓ 0 missing files<br />✓ 0 digest mismatches</p></div>
        <div className="nx-card"><h2>Parser result</h2><p>✓ 428 records imported<br />! 5 parser warnings<br />! 2 skipped records</p></div>
      </div>
      <Button variant="primary" onClick={onAnalyze}>Run Analysis</Button>
    </section>
  );
}

function AnalysisScreen({ onReview }) {
  return (
    <section className="nx-panel">
      <h1>Analyze Evidence</h1>
      <p className="nx-muted">Local deterministic analysis: regenerate traces, run deduplication, build workspace plan.</p>
      <SummaryCards />
      <Button variant="primary" onClick={onReview}>Open Review Queue</Button>
    </section>
  );
}

function ReviewQueueScreen({ onCompare }) {
  return (
    <section className="nx-panel">
      <h1>Review Queue</h1>
      <p className="nx-muted">4 blocking · 4 review required · 5 warnings</p>
      <div className="nx-grid">
        <div className="nx-list-item"><Pill tone="orange">Blocking</Pill><h2>Human merge decision required</h2><p>Rayyan pair across Scopus and Web of Science. Title similarity crossed threshold and DOI is missing on one side.</p><Button onClick={onCompare}>View comparison</Button></div>
        <div className="nx-list-item"><Pill tone="yellow">Warning</Pill><h2>Import warning: source-specific identifier</h2><p>3 records include provider-specific identifiers.</p></div>
      </div>
    </section>
  );
}

function DuplicateClustersScreen({ onCompare }) {
  return (
    <section className="nx-panel">
      <h1>Duplicate Clusters</h1>
      <SummaryCards />
      <div className="nx-table-card"><table className="nx-table"><thead><tr><th>ID</th><th>Representative</th><th>Evidence</th><th>Sources</th><th /></tr></thead><tbody>
        <tr><td className="nx-mono">cluster-0001</td><td>Rayyan — a web and mobile app</td><td>DOI match</td><td>Scopus + WoS</td><td><Button>Open</Button></td></tr>
        <tr><td className="nx-mono">dedup-candidate-0001</td><td>Rayyan pair</td><td>Title similarity 0.91</td><td>Scopus + WoS</td><td><Button onClick={onCompare}>Compare</Button></td></tr>
      </tbody></table></div>
    </section>
  );
}

function DuplicateDetailScreen() {
  return (
    <section className="nx-panel">
      <h1>Duplicate comparison <Pill tone="orange">Human review required</Pill></h1>
      <div className="nx-grid nx-two">
        <div className="nx-card"><Pill tone="blue">Left record</Pill><h2>Rayyan — a web and mobile app for systematic reviews</h2><p>2016 · Scopus · DOI present</p></div>
        <div className="nx-card"><Pill tone="blue">Right record</Pill><h2>Rayyan: a web and mobile application for systematic reviews</h2><p>2016 · Web of Science · DOI missing</p></div>
      </div>
      <div className="nx-grid nx-two">
        <div className="nx-card"><h2>Why Nexus flagged this</h2><p>✓ Title similarity crossed threshold<br />! Identifier missing on one side<br />✓ Sources are different</p></div>
        <div className="nx-card"><h2>Decision actions</h2><Button variant="locked">Accept merge — locked</Button> <Button variant="locked">Reject merge — locked</Button> <Button variant="locked">Mark unresolved — locked</Button><p className="nx-muted">Decision execution is not available in v0.</p></div>
      </div>
    </section>
  );
}

export default AppShell;
