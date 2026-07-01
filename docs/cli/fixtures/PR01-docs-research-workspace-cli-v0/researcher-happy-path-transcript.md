# Researcher Happy Path Transcript

```bash
mkdir "AI screening tools review"
cd "AI screening tools review"

nexus init --title "AI screening tools review"
nexus status

nexus import search ../exports/scopus.csv --source scopus --format csv --query-id search-001 --query "systematic review screening software"
nexus import search ../exports/wos.ris --source web-of-science --format ris --query-id search-002 --query "systematic review screening software"
nexus import search ../exports/openalex.ris --source openalex --format ris --query-id search-003 --query "systematic review screening software"

nexus verify
nexus analyze
nexus review
nexus clusters review
nexus clusters show dedup-candidate-0001
```

Expected researcher understanding:

```text
I created a local project folder.
I imported local search/export files.
Nexus verified the files and parser output.
Nexus analyzed deduplication evidence.
Nexus showed me which records require human review.
Nexus did not query live providers or execute merge decisions.
```
