<?php

declare(strict_types=1);

const GENERATOR_VERSION = 'screening-fulltext-v1';
const EXPECTED_CASE_COUNT = 26;

use Nexus\Dissemination\Application\Dto\FullTextResult;
use Nexus\Dissemination\Application\UseCase\RetrieveFullTextHandler;
use Nexus\Dissemination\Domain\FullText;
use Nexus\Dissemination\Domain\FullTextArtifactType;
use Nexus\Dissemination\Domain\FullTextStatus;
use Nexus\Dissemination\Domain\Port\DownloadResult;
use Nexus\Screening\Domain\CouncilDecisionAggregator;
use Nexus\Screening\Domain\ScreeningCriteria;
use Nexus\Screening\Domain\ScreeningDecision;
use Nexus\Screening\Domain\ScreeningRationale;
use Nexus\Screening\Domain\ScreeningStage;
use Nexus\Screening\Domain\ScreeningVote;

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
    throw new \RuntimeException('Source lock does not contain a valid php_reference repository and commit.');
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

if (!is_array($input['cases'] ?? null)) {
    throw new \RuntimeException('Input fixture must contain a cases array.');
}

if (count($input['cases']) !== EXPECTED_CASE_COUNT) {
    fwrite(STDERR, "Expected exactly ".EXPECTED_CASE_COUNT." cases, found ".count($input['cases']).".\n");
    exit(4);
}

$results = [];
foreach ($input['cases'] as $case) {
    $results[] = [
        'id' => $case['id'] ?? '',
        'operation' => $case['operation'] ?? 'unknown',
        'result' => executeCase($case),
    ];
}

$output = [
    'fixtureSetId' => $input['fixtureSetId'] ?? 'php-screening-fulltext-v1',
    'schemaVersion' => '1.0.0',
    'sourceKind' => 'pinned-php-observable-behavior',
    'sourceCommit' => $sourceCommit,
    'cases' => $results,
];

$outputBytes = encodeJson($output);
writeFile($options['output'], $outputBytes);

$manifest = [
    'fixtureSetId' => $input['fixtureSetId'] ?? 'php-screening-fulltext-v1',
    'schemaVersion' => '1.0.0',
    'sourceKind' => 'pinned-php-observable-behavior',
    'sourceRepository' => $sourceRepository,
    'sourceCommit' => $sourceCommit,
    'sourceRefs' => [
        'src/Dissemination/Application/Dto/FullTextResult.php',
        'src/Dissemination/Application/UseCase/RetrieveFullTextHandler.php',
        'src/Dissemination/Domain/FullText.php',
        'src/Dissemination/Domain/FullTextArtifactType.php',
        'src/Dissemination/Domain/FullTextStatus.php',
        'src/Dissemination/Domain/Port/DownloadResult.php',
        'src/Screening/Domain/CouncilDecisionAggregator.php',
        'src/Screening/Domain/ScreeningCriteria.php',
        'src/Screening/Domain/ScreeningDecision.php',
        'src/Screening/Domain/ScreeningRationale.php',
        'src/Screening/Domain/ScreeningStage.php',
        'src/Screening/Domain/ScreeningVote.php',
        'src/Screening/Domain/ScreeningVerdict.php',
    ],
    'generatorCommand' => 'php scripts/php-golden/screening-fulltext-export.php --php-reference "$PHP_REFERENCE" --source-lock specs/SOURCE.lock.json --input fixtures/php-golden/screening-fulltext/v1/input.json --comparison fixtures/php-golden/screening-fulltext/v1/comparison.json --output fixtures/php-golden/screening-fulltext/v1/expected.json --manifest fixtures/php-golden/screening-fulltext/v1/manifest.json',
    'generatorVersion' => GENERATOR_VERSION,
    'environmentAssumptions' => [
        'PHP 8.3 or later',
        'git is available',
        'PHP reference tracked files are clean',
        'no network access and no Composer/network mode',
        'no live provider/download invocation',
        'output uses UTF-8 JSON with LF line endings',
    ],
    'inputDigest' => 'sha256:'.hash('sha256', $inputBytes),
    'outputDigest' => 'sha256:'.hash('sha256', $outputBytes),
    'sourceLockDigest' => 'sha256:'.hash('sha256', $sourceLockBytes),
    'classificationDigest' => 'sha256:'.hash('sha256', $comparisonBytes),
    'ignoredNondeterminism' => [
        'generated council verdict ids',
        'council verdict timestamps',
        'handler durations',
    ],
    'comparisonRules' => [
        'compare deterministic enum values and normalized hashes',
        'compare screening verdicts as {decision, confidence, source, rationale, voteCounts, votes} only',
        'exercise private RetrieveFullTextHandler validation via ReflectionClass::newInstanceWithoutConstructor',
        'normalize non-finite numeric values before JSON serialization',
        'no runtime network calls in fixture generation',
    ],
];
writeFile($options['manifest'], encodeJson($manifest));

function executeCase(array $case): array
{
    $operation = $case['operation'] ?? '';

    return match ($operation) {
        'screening-decision-vocabulary' => runScreeningDecisionVocabulary(),
        'screening-decision-included-booleans' => runScreeningDecisionIncludedBooleans(),
        'screening-stage-vocabulary' => runScreeningStageVocabulary(),

        'screening-criteria-key-order-hash-equality',
        'screening-criteria-key-order-stable',
        'screening-criteria-key-order-hash-stable' => runScreeningCriteriaKeyOrderHash($case),

        'screening-criteria-list-order-hash-inequality',
        'screening-criteria-list-order-hash-different',
        'screening-criteria-list-order-hash-inequalities',
        'screening-criteria-list-order-hash',
        'screening-criteria-list-order-semantic' => runScreeningCriteriaListOrderHash($case),

        'screening-criteria-raw-sha256',
        'screening-criteria-raw-sha-256',
        'screening-criteria-raw-hash-vs-envelope' => runScreeningCriteriaRawSha($case),

        'screening-vote-confidence-below-rejection',
        'screening-vote-confidence-below',
        'screening-confidence-below-zero-rejected' => runScreeningVoteConfidenceRejected($case, -0.1),
        'screening-vote-confidence-above-rejection',
        'screening-vote-confidence-above',
        'screening-confidence-above-one-rejected' => runScreeningVoteConfidenceRejected($case, 1.1),
        'screening-vote-confidence-nan-accepted',
        'screening-nonfinite-confidence-accepted' => runScreeningVoteNanAccepted($case),

        'screening-council-unanimous-include',
        'screening-council-unanimous-final' => runScreeningCouncil($case),
        'screening-council-majority-exclude',
        'screening-council-majority-final' => runScreeningCouncil($case),
        'screening-council-include-exclude-conflict',
        'screening-council-conflict-final' => runScreeningCouncil($case),
        'screening-council-all-failed',
        'screening-council-all-failed-final' => runScreeningCouncil($case),

        'fulltext-artifact-type-vocabulary' => runFullTextArtifactTypeVocabulary(),
        'fulltext-status-vocabulary' => runFullTextStatusVocabulary(),
        'fulltext-failure-factory',
        'fulltext-failure-result' => runFullTextFailureFactory($case),
        'fulltext-skipped-factory',
        'fulltext-skipped-result' => runFullTextSkippedFactory($case),
        'fulltext-success-path-projection' => runFullTextSuccessPathProjection($case),
        'fulltext-success-missing-raw-byte-digest',
        'fulltext-success-missing-byte-digest' => runFullTextSuccessMissingRawByteDigest($case),
        'fulltext-runtime-retrieval-projection' => runFullTextRuntimeRetrievalProjection($case),
        'fulltext-derived-extraction-absent' => runFullTextDerivedExtractionAbsent($case),

        'fulltext-valid-pdf-validation' => runArtifactValidationCase($case),
        'fulltext-invalid-pdf-signature' => runArtifactValidationCase($case),
        'fulltext-oversized-artifact' => runArtifactValidationCase($case),
        'fulltext-valid-xml-validation' => runArtifactValidationCase($case),
        'fulltext-html-rejected-as-xml',
        'fulltext-html-not-fulltext-xml' => runArtifactValidationCase($case),
        'fulltext-empty-text-rejected',
        'fulltext-empty-text-artifact' => runArtifactValidationCase($case),

        default => throw new \InvalidArgumentException("Unknown operation {$operation}"),
    };
}

function runScreeningDecisionVocabulary(): array
{
    return [
        'values' => array_map(static fn (ScreeningDecision $value): string => $value->value, ScreeningDecision::cases()),
    ];
}

function runScreeningDecisionIncludedBooleans(): array
{
    $decisions = [
        ScreeningDecision::INCLUDE,
        ScreeningDecision::NEEDS_REVIEW,
        ScreeningDecision::EXCLUDE,
    ];

    return [
        'decision' => array_map(static fn (ScreeningDecision $decision): string => $decision->value, $decisions),
        'included' => array_map(static fn (ScreeningDecision $decision): bool => $decision->included(), $decisions),
    ];
}

function runScreeningStageVocabulary(): array
{
    return [
        'values' => array_map(static fn (ScreeningStage $stage): string => $stage->value, ScreeningStage::cases()),
    ];
}

function runScreeningCriteriaKeyOrderHash(array $case): array
{
    $left = ScreeningCriteria::fromArray($case['criteriaA'] ?? []);
    $right = ScreeningCriteria::fromArray($case['criteriaB'] ?? []);
    $leftHash = $left->hash();
    $rightHash = $right->hash();

    return [
        'hashLeft' => $leftHash,
        'hashRight' => $rightHash,
        'equal' => $leftHash === $rightHash,
        'normalizedLeft' => sanitizeFiniteNumbers($left->toArray()),
        'normalizedRight' => sanitizeFiniteNumbers($right->toArray()),
    ];
}

function runScreeningCriteriaListOrderHash(array $case): array
{
    $left = ScreeningCriteria::fromArray($case['criteriaA'] ?? []);
    $right = ScreeningCriteria::fromArray($case['criteriaB'] ?? []);
    $leftHash = $left->hash();
    $rightHash = $right->hash();

    return [
        'hashLeft' => $leftHash,
        'hashRight' => $rightHash,
        'equal' => $leftHash === $rightHash,
        'normalizedLeft' => sanitizeFiniteNumbers($left->toArray()),
        'normalizedRight' => sanitizeFiniteNumbers($right->toArray()),
    ];
}

function runScreeningCriteriaRawSha(array $case): array
{
    $criteria = ScreeningCriteria::fromArray($case['criteria'] ?? []);
    $normalized = $criteria->toArray();

    return [
        'hash' => 'sha256:'.hash('sha256', json_encode($normalized, JSON_THROW_ON_ERROR)),
        'normalized' => sanitizeFiniteNumbers($normalized),
        'criteriaHash' => $criteria->hash(),
    ];
}

function runScreeningVoteConfidenceRejected(array $case, float $forcedConfidence): array
{
    $confidence = is_numeric($case['confidence'] ?? null) ? (float) $case['confidence'] : $forcedConfidence;
    try {
        $vote = VotingFixture::vote(
            id: $case['voteId'] ?? ('vote-fixed-'.str_replace('.', '_', (string) $forcedConfidence)),
            decision: ScreeningDecision::INCLUDE,
            confidence: $confidence,
            attempt: 1,
            provider: $case['provider'] ?? 'provider-a',
            model: $case['model'] ?? 'model-a',
        );

        return [
            'accepted' => true,
            'decision' => $vote->decision?->value,
            'confidence' => sanitizeFiniteNumbers($vote->confidence),
            'expectedRejection' => 'rejected',
        ];
    } catch (\Throwable $error) {
        return [
            'accepted' => false,
            'errorCategory' => 'vote-confidence-rejected',
            'expectedRejection' => 'rejected',
            'message' => $error->getMessage(),
        ];
    }
}

function runScreeningVoteNanAccepted(array $case): array
{
    $confidence = parseFloatValue($case['confidence'] ?? 'NAN');

    $vote = VotingFixture::vote(
        id: $case['voteId'] ?? 'vote-nan-fixed',
        decision: ScreeningDecision::INCLUDE,
        confidence: $confidence,
        attempt: 1,
        provider: $case['provider'] ?? 'provider-nan',
        model: $case['model'] ?? 'model-nan',
    );

    return [
        'accepted' => $vote->succeeded(),
        'decision' => $vote->decision->value,
        'confidenceFinite' => is_finite($vote->confidence),
        'confidence' => sanitizeFiniteNumbers($vote->confidence),
    ];
}

function runScreeningCouncil(array $case): array
{
    $aggregator = new CouncilDecisionAggregator();
    $stage = ScreeningStage::from($case['stage'] ?? ScreeningStage::TITLE_ABSTRACT->value);
    $votes = [];
    foreach ($case['votes'] ?? [] as $index => $vote) {
        $id = $vote['voteId'] ?? 'vote-'.($index + 1);
        $model = $vote['modelId'] ?? 'model-'.($index + 1);
        if (isset($vote['error'])) {
            $votes[] = VotingFixture::failedVote($id, $stage, 1, 'fixture-provider', $model, $vote['error']);
            continue;
        }

        $votes[] = VotingFixture::vote(
            $id,
            ScreeningDecision::from($vote['decision']),
            (float) $vote['confidence'],
            1,
            'fixture-provider',
            $model,
        );
    }

    $verdict = $aggregator->aggregate(
        projectId: 'project-fixture',
        workId: $case['candidateId'] ?? 'candidate-fixture',
        stage: $stage,
        votes: $votes,
    );

    return normalizeVerdict($verdict);
}

function runFullTextArtifactTypeVocabulary(): array
{
    return [
        'values' => array_map(static fn (FullTextArtifactType $type): string => $type->value, FullTextArtifactType::cases()),
    ];
}

function runFullTextStatusVocabulary(): array
{
    return [
        'values' => array_map(static fn (FullTextStatus $status): string => $status->value, FullTextStatus::cases()),
    ];
}

function runFullTextFailureFactory(array $case): array
{
    $error = $case['errorCategory'] ?? 'full-text-failure';
    $source = sourceAlias($case);
    $http = isset($case['http']) ? (int) $case['http'] : null;
    $result = FullTextResult::failure($error, $source, $http);
    $legacy = FullText::failure($error, $source, $http);

    return normalizeFullTextResult($result) + ['legacy' => normalizeLegacyFullText($legacy)];
}

function runFullTextSkippedFactory(array $case): array
{
    $reason = $case['reason'] ?? 'skipped';
    $source = sourceAlias($case);
    $result = FullTextResult::skipped($reason, $source);
    $legacy = FullText::skipped($reason);

    return normalizeFullTextResult($result) + ['legacy' => normalizeLegacyFullText($legacy)];
}

function runFullTextSuccessPathProjection(array $case): array
{
    $path = $case['path'];
    $source = sourceAlias($case);
    $http = (int) ($case['http'] ?? 200);
    $result = FullTextResult::success($path, $source, $http);
    $legacy = FullText::success($path, $source, $http);

    return normalizeFullTextResult($result) + ['legacy' => normalizeLegacyFullText($legacy)];
}

function runFullTextSuccessMissingRawByteDigest(array $case): array
{
    $path = $case['path'];
    $source = sourceAlias($case);
    $http = (int) ($case['http'] ?? 200);
    $result = FullTextResult::success($path, $source, $http);
    $legacy = FullText::success($path, $source, $http);

    return normalizeFullTextResult($result) + ['legacy' => normalizeLegacyFullText($legacy)];
}

function runFullTextRuntimeRetrievalProjection(array $case): array
{
    $result = FullTextResult::success(
        $case['path'],
        sourceAlias($case),
        (int) ($case['http'] ?? 200),
        ['retrieved_by' => 'runtime-path'],
    );

    return [
        'projection' => [
            'path' => $result->filePath,
            'source' => $result->sourceAlias,
            'http' => $result->httpStatus,
        ],
        'hasRawBytes' => array_key_exists('raw_bytes', $result->metadata) && $result->metadata['raw_bytes'] !== null,
        'hasRawByteDigest' => array_key_exists('raw_byte_digest', $result->metadata) && $result->metadata['raw_byte_digest'] !== null,
        'hasLegacyBytes' => false,
        'hasLegacyDigest' => false,
        'sourceMeta' => $result->metadata['retrieved_by'] ?? null,
    ];
}

function runFullTextDerivedExtractionAbsent(array $case): array
{
    $result = FullTextResult::success($case['path'], sourceAlias($case), (int) ($case['http'] ?? 200));

    return [
        'fullTextResult' => normalizeFullTextResult($result),
        'fullTextExtractionRecordClassExists' => class_exists('Nexus\\Dissemination\\Domain\\FullTextExtractionRecord', false),
    ];
}

function runArtifactValidationCase(array $case): array
{
    $method = match ($case['artifactKind']) {
        'pdf' => 'assertValidPdf',
        'xml' => 'assertValidXml',
        'text' => 'assertValidText',
        default => throw new \InvalidArgumentException('Unsupported validation artifact kind.'),
    };

    return runArtifactValidation(
        method: $method,
        download: new DownloadResult(
            $case['bytes'],
            (int) ($case['http'] ?? 200),
            $case['mediaType'] ?? null,
        ),
        maxBytes: (int) $case['maxBytes'],
    );
}

function runArtifactValidation(string $method, DownloadResult $download, int $maxBytes): array
{
    $validator = getRetrieveFullTextValidationMethod($method);
    $handler = newEmptyRetrievalHandler();

    try {
        $validator->invokeArgs($handler, [$download, $maxBytes]);
        return ['accepted' => true, 'category' => null];
    } catch (\Throwable $error) {
        return [
            'accepted' => false,
            'errorCategory' => mapValidationErrorCategory($error->getMessage()),
            'message' => $error->getMessage(),
        ];
    }
}

function getRetrieveFullTextValidationMethod(string $method): \ReflectionMethod
{
    static $methods = [];
    static $handlerClass = null;

    if (!isset($methods[$method])) {
        if ($handlerClass === null) {
            $handlerClass = new \ReflectionClass(RetrieveFullTextHandler::class);
        }

        $methodObj = $handlerClass->getMethod($method);
        $methodObj->setAccessible(true);
        $methods[$method] = $methodObj;
    }

    return $methods[$method];
}

function newEmptyRetrievalHandler(): RetrieveFullTextHandler
{
    static $handler = null;

    if ($handler === null) {
        $handler = (new \ReflectionClass(RetrieveFullTextHandler::class))->newInstanceWithoutConstructor();
    }

    return $handler;
}

function mapValidationErrorCategory(string $message): string
{
    if (str_contains($message, 'missing %PDF signature')) {
        return 'invalid-pdf-signature';
    }

    if (str_contains($message, 'exceeds size limit')) {
        return 'artifact-too-large';
    }

    if (str_contains($message, 'received an HTML page')) {
        return 'html-not-fulltext-xml';
    }

    if (str_contains($message, 'not valid XML') || str_contains($message, 'not XML')) {
        return 'invalid-xml';
    }

    if (str_contains($message, 'Downloaded text is empty')) {
        return 'empty-text-artifact';
    }

    return 'validation-failed';
}

function normalizeVerdict($verdict): array
{
    $votes = [];
    foreach ($verdict->votes as $vote) {
        $votes[] = [
            'decision' => $vote->decision?->value,
            'confidence' => sanitizeFiniteNumbers($vote->confidence),
            'source' => $vote->provider . ':' . $vote->model,
            'rationale' => $vote->rationale->toArray(),
        ];
    }

    $counts = ['include' => 0, 'exclude' => 0, 'needs_review' => 0, 'failed' => 0];
    foreach ($verdict->votes as $vote) {
        if (! $vote->succeeded()) {
            ++$counts['failed'];
            continue;
        }

        $counts[$vote->decision?->value]++;
    }

    return [
        'decision' => $verdict->decision->value,
        'confidence' => sanitizeFiniteNumbers($verdict->confidence),
        'source' => $verdict->source,
        'rationale' => $verdict->rationale->toArray(),
        'voteCounts' => $counts,
        'votes' => $votes,
    ];
}

function sourceAlias(array $case): string
{
    $source = $case['source'] ?? 'fixture-source';
    if (is_array($source)) {
        return (string) ($source['id'] ?? $source['kind'] ?? 'fixture-source');
    }

    return (string) $source;
}

function normalizeFullTextResult(FullTextResult $result): array
{
    return [
        'status' => $result->status->value,
        'path' => $result->filePath,
        'source' => $result->sourceAlias,
        'errorMessage' => $result->errorMessage,
        'http' => $result->httpStatus,
        'hasRawBytes' => array_key_exists('raw_bytes', $result->metadata) && $result->metadata['raw_bytes'] !== null,
        'hasRawByteDigest' => array_key_exists('raw_byte_digest', $result->metadata) && $result->metadata['raw_byte_digest'] !== null,
    ];
}

function normalizeLegacyFullText(FullText $fullText): array
{
    return [
        'status' => $fullText->status->value,
        'path' => $fullText->filePath,
        'source' => $fullText->sourceAlias,
        'errorMessage' => $fullText->errorMessage,
        'http' => $fullText->httpStatus,
        'hasRawBytes' => false,
        'hasRawByteDigest' => false,
    ];
}

function makeWorkId(string $value): string
{
    return 'doi:'.$value;
}

function parseFloatValue(mixed $value): float
{
    if (is_float($value)) {
        return $value;
    }

    if (is_int($value)) {
        return (float) $value;
    }

    if (is_string($value)) {
        $normalized = strtolower(trim($value));
        if ($normalized === 'nan') {
            return NAN;
        }
        if (is_numeric($normalized)) {
            return (float) $normalized;
        }
    }

    return NAN;
}

function sanitizeFiniteNumbers(mixed $value): mixed
{
    if (is_array($value)) {
        $sanitized = [];
        foreach ($value as $key => $item) {
            $sanitized[$key] = sanitizeFiniteNumbers($item);
        }

        return $sanitized;
    }

    if (is_float($value) && !is_finite($value)) {
        return null;
    }

    return $value;
}

function readRequired(string $path, string $description): string
{
    $bytes = file_get_contents($path);
    if ($bytes === false) {
        throw new \RuntimeException("Unable to read {$description}.");
    }

    return $bytes;
}

function encodeJson(array $value): string
{
    return json_encode(sanitizeFiniteNumbers($value), JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE | JSON_THROW_ON_ERROR)."\n";
}

function writeFile(string $path, string $bytes): void
{
    $directory = dirname($path);
    if (!is_dir($directory) && !mkdir($directory, 0777, true) && !is_dir($directory)) {
        throw new \RuntimeException("Unable to create {$directory}.");
    }

    if (file_put_contents($path, $bytes) === false) {
        throw new \RuntimeException("Unable to write {$path}.");
    }
}

function runGit(string $workingDirectory, array $arguments): string
{
    $command = ['git', '-C', $workingDirectory, ...$arguments];
    $pipes = [];
    $process = proc_open($command, [1 => ['pipe', 'w'], 2 => ['pipe', 'w']], $pipes);
    if (!is_resource($process)) {
        throw new \RuntimeException('Unable to start git.');
    }

    $stdout = stream_get_contents($pipes[1]);
    $stderr = stream_get_contents($pipes[2]);
    fclose($pipes[1]);
    fclose($pipes[2]);

    if (proc_close($process) !== 0) {
        throw new \RuntimeException("git failed: {$stderr}");
    }

    return $stdout;
}

final class VotingFixture
{
    public static function vote(
        string $id,
        ScreeningDecision $decision,
        float $confidence,
        int $attempt,
        string $provider,
        string $model,
    ): ScreeningVote {
        return ScreeningVote::model(
            screeningRunId: 'run-fixed',
            projectId: 'project-fixed',
            workId: makeWorkId('10.0000/'.strtolower($id)),
            stage: ScreeningStage::FULL_TEXT,
            provider: $provider,
            model: $model,
            attempt: $attempt,
            decision: $decision,
            confidence: $confidence,
            rationale: new ScreeningRationale(
                reason: 'fixture',
                evidence: ['fixture-evidence'],
            ),
            id: $id,
        );
    }

    public static function failedVote(
        string $id,
        ScreeningStage $stage,
        int $attempt,
        string $provider,
        string $model,
        string $error,
    ): ScreeningVote {
        return ScreeningVote::failed(
            screeningRunId: 'run-fixed',
            projectId: 'project-fixed',
            workId: makeWorkId('10.0000/'.strtolower($id)),
            stage: $stage,
            provider: $provider,
            model: $model,
            attempt: $attempt,
            error: $error,
            id: $id,
        );
    }
}
