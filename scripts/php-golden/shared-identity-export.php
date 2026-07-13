<?php

declare(strict_types=1);

const GENERATOR_VERSION = 'shared-identity-v1';

use Nexus\Shared\Domain\CorpusSlice;
use Nexus\Shared\Domain\ScholarlyWork;
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

$sourceLockBytes = file_get_contents($options['source-lock']);
if ($sourceLockBytes === false) {
    throw new RuntimeException('Unable to read source lock.');
}
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

$inputBytes = file_get_contents($options['input']);
if ($inputBytes === false) {
    throw new RuntimeException('Unable to read fixture input.');
}

$input = json_decode($inputBytes, true, flags: JSON_THROW_ON_ERROR);
$comparisonBytes = file_get_contents($options['comparison']);
if ($comparisonBytes === false) {
    throw new RuntimeException('Unable to read semantic classifications.');
}
$results = [];
foreach ($input['cases'] as $case) {
    $results[] = [
        'id' => $case['id'],
        'operation' => $case['operation'],
        'result' => executeCase($case),
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
        'src/Shared/ValueObject/WorkId.php',
        'src/Shared/ValueObject/WorkIdNamespace.php',
        'src/Shared/ValueObject/WorkIdSet.php',
        'src/Shared/Domain/ScholarlyWork.php',
        'src/Shared/Domain/CorpusSlice.php',
        'tests/Unit/Shared/WorkIdTest.php',
        'tests/Unit/Shared/ScholarlyWorkTest.php',
    ],
    'generatorCommand' => 'php scripts/php-golden/shared-identity-export.php --php-reference "$PHP_REFERENCE" --source-lock specs/SOURCE.lock.json --input fixtures/php-golden/shared-identity/v1/input.json --comparison fixtures/php-golden/shared-identity/v1/comparison.json --output fixtures/php-golden/shared-identity/v1/expected.json --manifest fixtures/php-golden/shared-identity/v1/manifest.json',
    'generatorVersion' => GENERATOR_VERSION,
    'environmentAssumptions' => [
        'PHP 8.3 or later',
        'git is available',
        'PHP reference tracked files are clean',
        'no network access or Composer dependencies are required',
        'UTF-8 JSON with LF line endings',
    ],
    'inputDigest' => 'sha256:'.hash('sha256', $inputBytes),
    'outputDigest' => 'sha256:'.hash('sha256', $outputBytes),
    'sourceLockDigest' => 'sha256:'.hash('sha256', $sourceLockBytes),
    'classificationDigest' => 'sha256:'.hash('sha256', $comparisonBytes),
    'ignoredNondeterminism' => [],
    'comparisonRules' => [
        'compare normalized identifier strings exactly',
        'compare ordered result arrays only where the fixture operation defines order',
        'exclude generated corpus ids and retrieved timestamps from output',
        'require every case to have a reviewed semantic classification',
    ],
];
writeFile($options['manifest'], encodeJson($manifest));

function executeCase(array $case): array
{
    return match ($case['operation']) {
        'normalize-identifiers' => [
            'normalized' => array_map(
                static fn (array $id): string => makeWorkId($id)->toString(),
                $case['identifiers'],
            ),
        ],
        'parse-identifier' => ['normalized' => WorkId::fromString($case['value'])->toString()],
        'construct-identifier' => ['normalized' => makeWorkId($case['identifier'])->toString()],
        'primary-identifier' => ['primary' => makeWorkIdSet($case['identifiers'])->primary()?->toString()],
        'identifier-overlap' => [
            'overlap' => makeWorkIdSet($case['left'])->hasOverlapWith(makeWorkIdSet($case['right'])),
        ],
        'merge-identifier-sets' => [
            'ids' => array_map(
                static fn (WorkId $id): string => $id->toString(),
                makeWorkIdSet($case['left'])->merge(makeWorkIdSet($case['right']))->all(),
            ),
        ],
        'merge-works' => mergeWorks($case),
        'dedupe-corpus' => dedupeCorpus($case),
        'no-id-candidates' => noIdCandidates($case),
        'unsafe-same-instance' => unsafeSameInstance($case),
        'title-lookup' => titleLookup($case),
        default => throw new InvalidArgumentException("Unknown operation {$case['operation']}"),
    };
}

function mergeWorks(array $case): array
{
    $left = makeWork($case['left']);
    $right = makeWork($case['right']);
    $merged = $left->mergeWith($right);

    return [
        'sameWork' => $left->isSameWorkAs($right),
        'title' => $merged->title(),
        'ids' => array_map(static fn (WorkId $id): string => $id->toString(), $merged->ids()->all()),
    ];
}

function dedupeCorpus(array $case): array
{
    $slice = CorpusSlice::empty();
    foreach ($case['works'] as $work) {
        $slice = $slice->withWork(makeWork($work));
    }

    return [
        'count' => $slice->count(),
        'works' => array_map(static fn (ScholarlyWork $work): array => [
            'title' => $work->title(),
            'ids' => array_map(static fn (WorkId $id): string => $id->toString(), $work->ids()->all()),
        ], $slice->all()),
    ];
}

function noIdCandidates(array $case): array
{
    $left = makeWork($case['left']);
    $right = makeWork($case['right']);
    $slice = CorpusSlice::fromWorks($left, $right);

    return ['count' => $slice->count(), 'sameWork' => $left->isSameWorkAs($right)];
}

function unsafeSameInstance(array $case): array
{
    $work = makeWork($case['work']);
    return ['count' => CorpusSlice::fromWorksUnsafe($work, $work)->count()];
}

function titleLookup(array $case): array
{
    $slice = CorpusSlice::fromWorks(makeWork($case['work']));
    return ['foundTitle' => $slice->findByTitle($case['query'])?->title()];
}

function makeWork(array $definition): ScholarlyWork
{
    return ScholarlyWork::reconstitute(
        ids: makeWorkIdSet($definition['ids']),
        title: $definition['title'],
        sourceProvider: $definition['sourceProvider'],
    );
}

function makeWorkIdSet(array $definitions): WorkIdSet
{
    return WorkIdSet::fromArray(array_map(static fn (array $id): WorkId => makeWorkId($id), $definitions));
}

function makeWorkId(array $definition): WorkId
{
    $namespace = WorkIdNamespace::from($definition['namespace']);
    return new WorkId($namespace, $definition['value']);
}

function encodeJson(array $value): string
{
    return json_encode($value, JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES | JSON_THROW_ON_ERROR)."\n";
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
