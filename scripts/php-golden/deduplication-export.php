<?php

declare(strict_types=1);

const GENERATOR_VERSION = 'deduplication-v1';

use Nexus\Deduplication\Application\DeduplicateCorpus;
use Nexus\Deduplication\Application\DeduplicateCorpusHandler;
use Nexus\Deduplication\Application\DeduplicateCorpusResult;
use Nexus\Deduplication\Domain\DedupCluster;
use Nexus\Deduplication\Domain\Duplicate;
use Nexus\Deduplication\Domain\Port\DeduplicationPolicyPort;
use Nexus\Deduplication\Infrastructure\CompletenessElectionPolicy;
use Nexus\Deduplication\Infrastructure\DoiMatchPolicy;
use Nexus\Deduplication\Infrastructure\NamespaceMatchPolicy;
use Nexus\Deduplication\Infrastructure\TitleFuzzyPolicy;
use Nexus\Deduplication\Infrastructure\TitleNormalizer;
use Nexus\Shared\Application\CorpusLockPolicy;
use Nexus\Shared\Domain\CorpusSlice;
use Nexus\Shared\Domain\ScholarlyWork;
use Nexus\Shared\Exception\ProjectLockedException;
use Nexus\Shared\Port\CorpusSnapshotRepositoryPort;
use Nexus\Shared\Port\ProjectLockLifecyclePort;
use Nexus\Shared\Port\ProjectLockPort;
use Nexus\Shared\Port\ProjectWorkMembershipPort;
use Nexus\Shared\ValueObject\Author;
use Nexus\Shared\ValueObject\AuthorList;
use Nexus\Shared\ValueObject\CorpusSnapshot;
use Nexus\Shared\ValueObject\ProjectLockState;
use Nexus\Shared\ValueObject\Venue;
use Nexus\Shared\ValueObject\WorkId;
use Nexus\Shared\ValueObject\WorkIdNamespace;
use Nexus\Shared\ValueObject\WorkIdSet;

$options = getopt('', ['php-reference:', 'source-lock:', 'input:', 'comparison:', 'output:', 'manifest:']);
foreach (['php-reference', 'source-lock', 'input', 'comparison', 'output', 'manifest'] as $required) {
    if (!isset($options[$required]) || !is_string($options[$required])) {
        fwrite(STDERR, "Missing --{$required}.\n");
        exit(2);
    }
}

$reference = realpath($options['php-reference']);
if ($reference === false) {
    fwrite(STDERR, "PHP reference path does not exist.\n");
    exit(2);
}

$sourceLockBytes = readRequired($options['source-lock'], 'source lock');
$sourceLock = json_decode($sourceLockBytes, true, flags: JSON_THROW_ON_ERROR);
$sourceRepository = $sourceLock['php_reference']['repository'] ?? null;
$sourceCommit = $sourceLock['php_reference']['commit'] ?? null;
if (!is_string($sourceRepository) || !is_string($sourceCommit)) {
    throw new RuntimeException('Source lock does not contain a valid php_reference repository and commit.');
}

$head = trim(runGit($reference, ['rev-parse', 'HEAD']));
if ($head !== $sourceCommit) {
    fwrite(STDERR, "Refusing generation: expected {$sourceCommit}, found {$head}.\n");
    exit(3);
}
if (trim(runGit($reference, ['status', '--porcelain', '--untracked-files=no'])) !== '') {
    fwrite(STDERR, "Refusing generation from a dirty PHP reference worktree.\n");
    exit(3);
}

spl_autoload_register(static function (string $class) use ($reference): void {
    $prefix = 'Nexus\\';
    if (!str_starts_with($class, $prefix)) {
        return;
    }

    $path = $reference.'/src/'.str_replace('\\', '/', substr($class, strlen($prefix))).'.php';
    if (is_file($path)) {
        require_once $path;
    }
});

$inputBytes = readRequired($options['input'], 'fixture input');
$comparisonBytes = readRequired($options['comparison'], 'semantic classifications');
$input = json_decode($inputBytes, true, flags: JSON_THROW_ON_ERROR);

$policies = [
    'doi_match' => new DoiMatchPolicy(),
    'arxiv_match' => new NamespaceMatchPolicy(WorkIdNamespace::ARXIV),
    'openalex_match' => new NamespaceMatchPolicy(WorkIdNamespace::OPENALEX),
    's2_match' => new NamespaceMatchPolicy(WorkIdNamespace::S2),
    'pubmed_match' => new NamespaceMatchPolicy(WorkIdNamespace::PUBMED),
    'title_fuzzy_default_92' => new TitleFuzzyPolicy(new TitleNormalizer(), 92),
];

$results = [];
foreach ($input['cases'] as $case) {
    $results[] = [
        'id' => $case['id'],
        'operation' => $case['operation'],
        'result' => executeCase($case, $policies),
    ];
}

$output = [
    'fixtureSetId' => $input['fixtureSetId'],
    'schemaVersion' => '1.0.0',
    'sourceKind' => 'pinned-php-observable-behavior',
    'sourceCommit' => $sourceCommit,
    'cases' => $results,
];
$outputBytes = encodeJson($output);
writeFile($options['output'], $outputBytes);

$manifest = [
    'fixtureSetId' => $input['fixtureSetId'],
    'schemaVersion' => '1.0.0',
    'sourceKind' => 'pinned-php-observable-behavior',
    'sourceRepository' => $sourceRepository,
    'sourceCommit' => $sourceCommit,
    'sourceRefs' => [
        'src/Deduplication/Application/DeduplicateCorpus.php',
        'src/Deduplication/Application/DeduplicateCorpusHandler.php',
        'src/Deduplication/Application/DeduplicateCorpusResult.php',
        'src/Deduplication/Domain/DedupCluster.php',
        'src/Deduplication/Domain/DedupClusterCollection.php',
        'src/Deduplication/Domain/Duplicate.php',
        'src/Deduplication/Domain/DuplicateReason.php',
        'src/Deduplication/Domain/Port/DeduplicationPolicyPort.php',
        'src/Deduplication/Domain/Port/RepresentativeElectionPort.php',
        'src/Deduplication/Infrastructure/CompletenessElectionPolicy.php',
        'src/Deduplication/Infrastructure/DoiMatchPolicy.php',
        'src/Deduplication/Infrastructure/NamespaceMatchPolicy.php',
        'src/Deduplication/Infrastructure/TitleFuzzyPolicy.php',
        'src/Deduplication/Infrastructure/TitleNormalizer.php',
        'src/Deduplication/Infrastructure/UnionFind.php',
        'src/Shared/Application/CorpusLockPolicy.php',
        'src/Shared/Exception/ProjectLockedException.php',
        'src/Shared/Port/CorpusSnapshotRepositoryPort.php',
        'src/Shared/Port/ProjectLockLifecyclePort.php',
        'src/Shared/Port/ProjectLockPort.php',
        'src/Shared/Port/ProjectWorkMembershipPort.php',
        'src/Shared/ValueObject/CorpusSnapshot.php',
        'src/Shared/ValueObject/CorpusOperation.php',
        'src/Shared/ValueObject/ProjectLockState.php',
        'src/Shared/Domain/CorpusSlice.php',
        'src/Shared/Domain/ScholarlyWork.php',
        'src/Shared/ValueObject/Author.php',
        'src/Shared/ValueObject/AuthorList.php',
        'src/Shared/ValueObject/WorkId.php',
        'src/Shared/ValueObject/WorkIdNamespace.php',
        'src/Shared/ValueObject/WorkIdSet.php',
        'src/Shared/ValueObject/Venue.php',
        'tests/Unit/Deduplication/DeduplicationTest.php',
        'tests/Unit/Deduplication/UnionFindTest.php',
        'tests/Unit/Shared/CorpusLockPolicyTest.php',
    ],
    'generatorCommand' => 'php scripts/php-golden/deduplication-export.php --php-reference "$PHP_REFERENCE" --source-lock specs/SOURCE.lock.json --input fixtures/php-golden/deduplication/v1/input.json --comparison fixtures/php-golden/deduplication/v1/comparison.json --output fixtures/php-golden/deduplication/v1/expected.json --manifest fixtures/php-golden/deduplication/v1/manifest.json',
    'generatorVersion' => GENERATOR_VERSION,
    'environmentAssumptions' => [
        'PHP 8.3 or later',
        'git is available',
        'PHP reference tracked files are clean',
        'no network access or Composer dependencies are required',
        'title-fuzzy defaults use TitleFuzzyPolicy threshold 92 unless overridden',
        'stable fixture seed data avoids runtime-only identifiers',
        'UTF-8 JSON with LF line endings',
    ],
    'inputDigest' => 'sha256:'.hash('sha256', $inputBytes),
    'outputDigest' => 'sha256:'.hash('sha256', $outputBytes),
    'sourceLockDigest' => 'sha256:'.hash('sha256', $sourceLockBytes),
    'classificationDigest' => 'sha256:'.hash('sha256', $comparisonBytes),
    'ignoredNondeterminism' => [
        'generated cluster ids',
        'runtime object hashes used for internal keying',
        'retrieved timestamps',
        'durationMs',
    ],
    'comparisonRules' => [
        'compare serialized member identifiers as normalized identifier sets',
        'compare duplicate evidence as normalized (primary, secondary, reason, confidence) tuples',
        'compare cluster member sets and counts; reason/confidence values are exact',
        'ignore generated cluster IDs and object hashes in semantic classification',
        'ignore retrieved timestamp fields unless the fixture explicitly pins them',
    ],
];
writeFile($options['manifest'], encodeJson($manifest));

function executeCase(array $case, array $policies): array
{
    return match ($case['operation']) {
        'exact-doi-policy' => runExactPolicyCase($case, $policies['doi_match']),
        'exact-arxiv-policy' => runExactPolicyCase($case, $policies['arxiv_match']),
        'exact-openalex-policy' => runExactPolicyCase($case, $policies['openalex_match']),
        'exact-s2-policy' => runExactPolicyCase($case, $policies['s2_match']),
        'exact-pubmed-policy' => runExactPolicyCase($case, $policies['pubmed_match']),
        'empty-handler' => runDedupCase(case: $case, policies: [
            $policies['doi_match'],
            $policies['arxiv_match'],
            $policies['openalex_match'],
            $policies['s2_match'],
            $policies['pubmed_match'],
            $policies['title_fuzzy_default_92'],
        ]),
        'singleton-handler' => runDedupCase(case: $case, policies: [$policies['doi_match'], $policies['arxiv_match'], $policies['openalex_match'], $policies['s2_match'], $policies['pubmed_match']]),
        'transitive-handler' => runDedupCase(case: $case, policies: [
            $policies['arxiv_match'],
            $policies['s2_match'],
        ]),
        'representative-merge-handler' => runRepresentativeMergeCase(case: $case, policies: [$policies['arxiv_match']]),
        'title-default-92-vs-95' => runTitleThresholdComparison($case),
        'title-explicit-95-auto-cluster' => runDedupCase(
            case: $case,
            policies: [new TitleFuzzyPolicy(new TitleNormalizer(), 95)],
        ),
        'no-id-runtime-fallback' => runDedupCase(
            case: $case,
            policies: [new TitleFuzzyPolicy(new TitleNormalizer(), 92)],
        ),
        'corpus-slice-construction' => runCorpusSliceConstructionCase($case),
        'locked-dedup-rejected' => runLockedDedupRejectionCase($case),
        'lock-export-with-snapshot' => runLockExportCase($case, includeSnapshot: true),
        'lock-export-without-snapshot' => runLockExportCase($case, includeSnapshot: false),
        default => throw new InvalidArgumentException("Unknown operation {$case['operation']}"),
    };
}

function runExactPolicyCase(array $case, DeduplicationPolicyPort $policy): array
{
    $works = array_map(static fn (array $definition): ScholarlyWork => makeWork($definition), $case['works']);
    $duplicates = array_map(static fn (Duplicate $duplicate): array => normalizeDuplicate($duplicate), $policy->detect($works));

    return [
        'policy' => $policy->name(),
        'duplicateCount' => count($duplicates),
        'duplicates' => $duplicates,
    ];
}

function runTitleThresholdComparison(array $case): array
{
    $works = array_map(static fn (array $definition): ScholarlyWork => makeWork($definition), $case['works']);
    $normalizer = new TitleNormalizer();

    return [
        'threshold92DuplicateCount' => count((new TitleFuzzyPolicy($normalizer, 92))->detect($works)),
        'threshold95DuplicateCount' => count((new TitleFuzzyPolicy($normalizer, 95))->detect($works)),
        'similarityRatio' => $normalizer->fuzzyRatio($case['works'][0]['title'], $case['works'][1]['title']),
    ];
}

function runCorpusSliceConstructionCase(array $case): array
{
    $works = array_map(static fn (array $definition): ScholarlyWork => makeWork($definition), $case['works']);

    return [
        'safeCount' => CorpusSlice::fromWorks(...$works)->count(),
        'unsafeCount' => CorpusSlice::fromWorksUnsafe(...$works)->count(),
    ];
}

function runDedupCase(
    array $case,
    array $policies,
    bool $useUnsafeSlice = true,
): array {
    $works = array_map(static fn (array $definition): ScholarlyWork => makeWork($definition), $case['works'] ?? []);
    $slice = buildCorpusSlice($works, $useUnsafeSlice);
    $handler = new DeduplicateCorpusHandler($policies, new CompletenessElectionPolicy());
    $result = $handler->handle(new DeduplicateCorpus($slice, projectId: $case['projectId'] ?? 'default-project'));

    return normalizeDedupResult($result);
}

function runRepresentativeMergeCase(array $case, array $policies): array
{
    $base = runDedupCase($case, $policies);
    if (empty($base['clusters'])) {
        return $base;
    }

    $works = array_map(static fn (array $definition): ScholarlyWork => makeWork($definition), $case['works']);
    $slice = buildCorpusSlice($works, true);
    $handler = new DeduplicateCorpusHandler($policies, new CompletenessElectionPolicy());
    $result = $handler->handle(new DeduplicateCorpus($slice, $case['projectId'] ?? 'default-project'));
    $clusters = $result->clusters->all();

    $fused = null;
    $representatives = [];
    if ($clusters !== []) {
        $cluster = $clusters[0];
        $members = $cluster->members();
        $representative = $cluster->representative();

        if ($representative !== null) {
            foreach ($cluster->nonRepresentatives() as $member) {
                $representative = $representative->mergeWith($member);
            }

            $fused = normalizeWorkSummary($representative);
            $representatives[] = $representative->title();
        }
    }

    $base['fusedRepresentative'] = $fused;
    $base['representativeElectionSource'] = 'CompletenessElectionPolicy';
    return $base;
}

function runLockedDedupRejectionCase(array $case): array
{
    $fakeMembership = new class implements ProjectWorkMembershipPort {
        public function missingWorkIds(string $projectId, array $workIds): array
        {
            return [];
        }
    };
    $fakeLocks = new class implements ProjectLockPort {
        public function isLocked(string $projectId): bool
        {
            return true;
        }
    };
    $policy = new CorpusLockPolicy(
        $fakeLocks,
        $fakeMembership,
    );
    $handler = new DeduplicateCorpusHandler(
        [
            new DoiMatchPolicy(),
            new NamespaceMatchPolicy(WorkIdNamespace::ARXIV),
            new NamespaceMatchPolicy(WorkIdNamespace::OPENALEX),
            new NamespaceMatchPolicy(WorkIdNamespace::S2),
            new NamespaceMatchPolicy(WorkIdNamespace::PUBMED),
        ],
        new CompletenessElectionPolicy(),
        $policy,
    );

    try {
        $slice = CorpusSlice::fromWorksUnsafe();
        foreach (($case['works'] ?? []) as $definition) {
            $slice = $slice->withWork(makeWork($definition));
        }
        $handler->handle(new DeduplicateCorpus($slice, projectId: $case['projectId'] ?? 'default-project'));
    } catch (ProjectLockedException $error) {
        return [
            'accepted' => false,
            'errorCategory' => 'project-locked',
            'error' => $error->getMessage(),
        ];
    }

    return [
        'accepted' => true,
        'error' => 'expected lock exception was not thrown',
    ];
}

function runLockExportCase(array $case, bool $includeSnapshot): array
{
    $projectId = $case['projectId'] ?? 'default-project';
    $snapshot = $includeSnapshot ? new CorpusSnapshot(
        'snapshot-'.$projectId.'-001',
        $projectId,
        new DateTimeImmutable('2026-01-01T00:00:00+00:00'),
        2,
        'fixture',
        'fixture-seed',
        ['seed' => 'php-dedup-fixture'],
        new DateTimeImmutable('2026-01-01T00:00:00+00:00'),
    ) : null;
    $locks = new class implements ProjectLockPort {
        public function isLocked(string $projectId): bool
        {
            return true;
        }
    };
    $membership = new class implements ProjectWorkMembershipPort {
        public function missingWorkIds(string $projectId, array $workIds): array
        {
            return [];
        }
    };
    $lifecycle = new class implements ProjectLockLifecyclePort {
        public function lock(string $projectId, ?string $actorId = null, ?string $reason = null, array $metadata = []): ProjectLockState
        {
            return $this->status($projectId);
        }

        public function unlock(string $projectId, ?string $actorId = null, ?string $reason = null, array $metadata = []): ProjectLockState
        {
            return new ProjectLockState($projectId, false, 'unlocked');
        }

        public function status(string $projectId): ProjectLockState
        {
            return new ProjectLockState($projectId, true, 'locked', new DateTimeImmutable('2026-01-01T00:00:00+00:00'), 'fixture', 'fixture-lock');
        }
    };
    $snapshotRepo = new class($snapshot) implements CorpusSnapshotRepositoryPort {
        public function __construct(private readonly ?CorpusSnapshot $snapshot)
        {
        }

        public function createForLockedProject(string $projectId, DateTimeImmutable $lockedAt, ?string $actorId = null, ?string $reason = null, array $metadata = []): CorpusSnapshot
        {
            return $this->snapshot ?? new CorpusSnapshot('created-snapshot', $projectId, $lockedAt, 0, $actorId, $reason, $metadata);
        }

        public function latestForProject(string $projectId): ?CorpusSnapshot
        {
            return $this->snapshot?->projectId === $projectId ? $this->snapshot : null;
        }
    };
    $lockPolicy = new CorpusLockPolicy($locks, $membership, $lifecycle, $snapshotRepo);
    $metadata = $lockPolicy->exportMetadata($projectId);

    return [
        'projectId' => $projectId,
        'lockMetadata' => [
            'project_locked' => $metadata['project_locked'],
            'lock_status' => $metadata['lock_status'],
            'snapshot_present' => $metadata['corpus_snapshot_id'] !== null,
            'snapshot_work_count' => $metadata['snapshot_work_count'],
            'citable' => $metadata['citable'],
            'final' => $metadata['final'],
        ],
    ];
}

function normalizeDedupResult(DeduplicateCorpusResult $result): array
{
    $clusters = $result->clusters->all();
    usort($clusters, static function (DedupCluster $a, DedupCluster $b): int {
        $aRep = $a->representative();
        $bRep = $b->representative();
        $aLabel = $aRep === null ? '' : implode('|', normalizeWorkIds($aRep));
        $bLabel = $bRep === null ? '' : implode('|', normalizeWorkIds($bRep));

        return $aLabel <=> $bLabel;
    });

    $normalizedClusters = [];
    foreach ($clusters as $cluster) {
        $members = [];
        foreach ($cluster->members() as $member) {
            $members[] = [
                'ids' => normalizeWorkIds($member),
                'title' => $member->title(),
                'provider' => $member->sourceProvider(),
            ];
        }
        usort($members, static function (array $a, array $b): int {
            return json_encode($a['ids']) <=> json_encode($b['ids']);
        });

        $evidence = [];
        $reasons = [];
        foreach ($cluster->duplicateEvidence() as $duplicate) {
            $normalized = normalizeDuplicate($duplicate);
            $evidence[] = $normalized;
            $reasons[] = $normalized['reason'];
        }
        sort($reasons);

        usort($evidence, static function (array $a, array $b): int {
            return $a['primary'] <=> $b['primary'];
        });

        $representative = $cluster->representative();
        $normalizedClusters[] = [
            'representative' => $representative === null ? null : normalizeWorkSummary($representative),
            'memberIdentifierSets' => array_map(
                static fn (array $member): array => $member['ids'],
                $members,
            ),
            'memberTitles' => array_map(static fn (array $member): string => $member['title'], $members),
            'memberProviders' => array_map(static fn (array $member): string => $member['provider'], $members),
            'duplicateReasons' => array_values(array_unique($reasons)),
            'duplicateCount' => count($evidence),
            'evidence' => $evidence,
            'memberCount' => count($members),
        ];
    }

    return [
        'inputCount' => $result->inputCount,
        'uniqueCount' => $result->uniqueCount,
        'duplicatesRemoved' => $result->duplicatesRemoved,
        'policyStats' => $result->policyStats,
        'clusterCount' => count($normalizedClusters),
        'clusters' => $normalizedClusters,
    ];
}

function normalizeWorkSummary(ScholarlyWork $work): array
{
    return [
        'ids' => normalizeWorkIds($work),
        'title' => $work->title(),
        'provider' => $work->sourceProvider(),
        'year' => $work->year(),
        'hasAbstract' => $work->abstract() !== null,
        'hasVenue' => $work->venue() !== null,
        'hasCitedBy' => $work->citedByCount(),
    ];
}

function normalizeWorkIds(ScholarlyWork $work): array
{
    $ids = array_map(static fn (WorkId $id): string => $id->toString(), $work->ids()->all());
    sort($ids, SORT_STRING);

    return $ids;
}

function normalizeDuplicate(Duplicate $duplicate): array
{
    return [
        'primary' => $duplicate->primaryId->toString(),
        'secondary' => $duplicate->secondaryId->toString(),
        'reason' => $duplicate->reason->value,
        'confidence' => $duplicate->confidence,
    ];
}

function buildCorpusSlice(array $works, bool $useUnsafe): CorpusSlice
{
    if ($works === []) {
        return CorpusSlice::empty();
    }

    return $useUnsafe
        ? CorpusSlice::fromWorksUnsafe(...$works)
        : CorpusSlice::fromWorks(...$works);
}

function makeWork(array $definition): ScholarlyWork
{
    $ids = array_map(
        static fn (array $definition): WorkId => new WorkId(WorkIdNamespace::from($definition['namespace']), $definition['value']),
        $definition['ids'] ?? [],
    );

    $authors = [];
    if (($definition['authors'] ?? []) !== []) {
        foreach ($definition['authors'] as $author) {
            $authors[] = new Author($author['familyName'], $author['givenName'] ?? null, null, $author['normalizedFullName'] ?? null);
        }
    }

    $venue = null;
    if (isset($definition['venue'])) {
        $venueData = $definition['venue'];
        if (is_array($venueData) && isset($venueData['name'])) {
            $venue = new Venue(
                $venueData['name'],
                $venueData['issn'] ?? null,
                $venueData['type'] ?? null,
                $venueData['publisher'] ?? null,
            );
        }
    }

    return ScholarlyWork::reconstitute(
        ids: WorkIdSet::fromArray($ids),
        title: $definition['title'],
        sourceProvider: $definition['sourceProvider'],
        year: $definition['year'] ?? null,
        authors: AuthorList::fromArray($authors),
        venue: $venue,
        abstract: $definition['abstract'] ?? null,
        citedByCount: $definition['citedByCount'] ?? null,
        isRetracted: $definition['isRetracted'] ?? false,
        rawData: $definition['rawData'] ?? null,
    );
}

function readRequired(string $path, string $description): string
{
    $bytes = file_get_contents($path);
    if ($bytes === false) {
        throw new RuntimeException("Unable to read {$description}.");
    }

    return $bytes;
}

function encodeJson(array $value): string
{
    return json_encode($value, JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE | JSON_THROW_ON_ERROR)."\n";
}

function writeFile(string $path, string $bytes): void
{
    $directory = dirname($path);
    if (!is_dir($directory) && !mkdir($directory, 0777, true) && !is_dir($directory)) {
        throw new RuntimeException("Unable to create {$directory}.");
    }

    if (file_put_contents($path, $bytes) === false) {
        throw new RuntimeException("Unable to write {$path}.");
    }
}

function runGit(string $workingDirectory, array $arguments): string
{
    $command = ['git', '-C', $workingDirectory, ...$arguments];
    $pipes = [];
    $process = proc_open($command, [1 => ['pipe', 'w'], 2 => ['pipe', 'w']], $pipes);
    if (!is_resource($process)) {
        throw new RuntimeException('Unable to start git.');
    }

    $stdout = stream_get_contents($pipes[1]);
    $stderr = stream_get_contents($pipes[2]);
    fclose($pipes[1]);
    fclose($pipes[2]);

    $exitCode = proc_close($process);
    if ($exitCode !== 0) {
        throw new RuntimeException("git failed: {$stderr}");
    }

    return $stdout;
}
