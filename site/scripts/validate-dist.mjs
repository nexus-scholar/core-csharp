import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const siteRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const pagesRoot = path.join(siteRoot, "src", "pages");
const distRoot = path.join(siteRoot, "dist");

function walkFiles(root) {
  const files = [];
  for (const entry of fs.readdirSync(root, { withFileTypes: true })) {
    const absolute = path.join(root, entry.name);
    if (entry.isDirectory()) {
      files.push(...walkFiles(absolute));
    } else if (entry.isFile()) {
      files.push(absolute);
    }
  }

  return files;
}

function relativePosix(root, file) {
  return path.relative(root, file).split(path.sep).join("/");
}

function expectedOutputForPage(page) {
  const relative = relativePosix(pagesRoot, page);
  if (relative === "404.astro") {
    return "404.html";
  }

  if (relative.endsWith("/index.astro")) {
    return relative.replace(/\.astro$/, ".html");
  }

  if (relative === "index.astro") {
    return "index.html";
  }

  return relative.replace(/\.astro$/, "/index.html");
}

function resolveLocalReference(sourceFile, reference) {
  const [withoutFragment, fragment] = reference.split("#", 2);
  const withoutQuery = withoutFragment.split("?", 1)[0];
  const decoded = decodeURI(withoutQuery);
  const candidate = decoded.startsWith("/")
    ? path.join(distRoot, decoded.slice(1))
    : path.resolve(path.dirname(sourceFile), decoded);

  if (decoded === "") {
    return { target: sourceFile, fragment };
  }

  const target =
    fs.existsSync(candidate) && fs.statSync(candidate).isDirectory()
      ? path.join(candidate, "index.html")
      : candidate;

  return { target, fragment };
}

if (!fs.existsSync(distRoot)) {
  throw new Error("site/dist does not exist. Run `npm run build` first.");
}

const pageSources = walkFiles(pagesRoot).filter((file) => file.endsWith(".astro"));
const expectedHtml = pageSources.map(expectedOutputForPage).sort();
const actualHtml = walkFiles(distRoot)
  .filter((file) => file.endsWith(".html"))
  .map((file) => relativePosix(distRoot, file))
  .sort();
const issues = [];

for (const expected of expectedHtml) {
  if (!actualHtml.includes(expected)) {
    issues.push(`missing generated route: ${expected}`);
  }
}

for (const actual of actualHtml) {
  if (!expectedHtml.includes(actual)) {
    issues.push(`unexpected generated route: ${actual}`);
  }
}

const idIndex = new Map();
for (const relative of actualHtml) {
  const absolute = path.join(distRoot, ...relative.split("/"));
  const html = fs.readFileSync(absolute, "utf8");
  idIndex.set(
    absolute,
    new Set([...html.matchAll(/\sid=["']([^"']+)["']/g)].map((match) => match[1])),
  );
}

let references = 0;
let descriptions = 0;
for (const relative of actualHtml) {
  const absolute = path.join(distRoot, ...relative.split("/"));
  const html = fs.readFileSync(absolute, "utf8");

  if (/<meta\s+name=["']description["']/i.test(html)) {
    descriptions += 1;
  } else if (relative !== "404.html") {
    issues.push(`missing meta description: ${relative}`);
  }

  if (html.includes("https://github.com/nexus-scholar/core-csharp")) {
    issues.push(`stale repository owner URL: ${relative}`);
  }

  if (html.includes("https://nexus-scholar.github.io/core-csharp")) {
    issues.push(`stale Pages URL: ${relative}`);
  }

  if (html.includes("is:inline")) {
    issues.push(`Astro compiler directive leaked into output: ${relative}`);
  }

  for (const match of html.matchAll(/(?:href|src)=["']([^"']+)["']/g)) {
    const reference = match[1];
    references += 1;
    if (
      /^(?:https?:|mailto:|tel:|data:|javascript:)/i.test(reference) ||
      reference === "#"
    ) {
      continue;
    }

    const { target, fragment } = resolveLocalReference(absolute, reference);
    if (!target.startsWith(distRoot + path.sep) && target !== distRoot) {
      issues.push(`${relative} -> reference escapes dist: ${reference}`);
      continue;
    }

    if (!fs.existsSync(target)) {
      issues.push(`${relative} -> missing target: ${reference}`);
      continue;
    }

    if (
      fragment &&
      target.endsWith(".html") &&
      !idIndex.get(target)?.has(decodeURIComponent(fragment))
    ) {
      issues.push(`${relative} -> missing anchor: ${reference}`);
    }
  }
}

const home = fs.readFileSync(path.join(distRoot, "index.html"), "utf8");
if (!home.includes('href="https://nexus.mouadh.org/"')) {
  issues.push("home page canonical URL is missing or incorrect");
}

if (!home.includes("github.com/nexus-scholar-org/core-csharp")) {
  issues.push("home page does not link to the organization repository");
}

const sitemap = fs.readFileSync(path.join(distRoot, "sitemap.xml"), "utf8");
const sitemapUrls = [...sitemap.matchAll(/<loc>([^<]+)<\/loc>/g)].map(
  (match) => match[1],
);
if (sitemapUrls.length !== expectedHtml.length - 1) {
  issues.push(
    `sitemap contains ${sitemapUrls.length} URLs; expected ${expectedHtml.length - 1}`,
  );
}

for (const url of sitemapUrls) {
  if (!url.startsWith("https://nexus.mouadh.org/")) {
    issues.push(`sitemap URL is outside the canonical domain: ${url}`);
  }
}

console.log(
  JSON.stringify(
    {
      generatedPages: actualHtml.length,
      metaDescriptions: descriptions,
      sitemapUrls: sitemapUrls.length,
      localReferences: references,
      issues: issues.length,
    },
    null,
    2,
  ),
);

if (issues.length > 0) {
  console.error(issues.slice(0, 100).join("\n"));
  process.exitCode = 1;
}
