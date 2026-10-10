#!/usr/bin/env fish

set -l version v0.111.0
if test (count $argv) -ge 1
    set version $argv[1]
end

if test "$version" != "v0.111.0"
    echo "No pinned Spire Codex manifest is registered for $version." >&2
    echo "Add an explicit source commit and blob manifest before syncing a new version." >&2
    exit 2
end

set -l source_commit b59c6f96043051afee8683237d04f943309ced35
set -l manifest "tools/reference_source/spire_codex_v0.111.0_git_blobs.tsv"
set -l out_dir "data/external/spire-codex/$version/eng"

if not command -q curl
    echo "curl is required." >&2
    exit 1
end

if not command -q git
    echo "git is required for blob verification." >&2
    exit 1
end

if not test -f "$manifest"
    echo "Pinned corpus manifest is missing: $manifest" >&2
    exit 1
end

mkdir -p "$out_dir"

while read -l file expected_blob
    if test -z "$file"
        continue
    end

    if string match -q '#*' -- "$file"
        continue
    end

    set -l url         "https://raw.githubusercontent.com/ptrlrd/spire-codex/$source_commit/data-beta/$version/eng/$file"
    set -l destination "$out_dir/$file"

    echo "Fetching $file"
    curl --fail --location --silent --show-error         "$url"         --output "$destination"
    or exit 1

    set -l actual_blob (git hash-object "$destination")
    if test "$actual_blob" != "$expected_blob"
        echo "Blob verification failed for $file" >&2
        echo "  expected: $expected_blob" >&2
        echo "  actual:   $actual_blob" >&2
        exit 1
    end
end < "$manifest"

printf '%s\n'     "Local research cache only."     "Source repository: ptrlrd/spire-codex"     "Source commit: $source_commit"     "Dataset: data-beta/$version/eng"     "Verified against: $manifest"     "Game data is © Mega Crit Games; do not commit or redistribute this cache."     > "$out_dir/SOURCE.txt"

echo "Synced and verified Spire Codex $version to $out_dir"
