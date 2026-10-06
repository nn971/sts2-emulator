#!/usr/bin/env fish

set -l version v0.111.0
if test (count $argv) -ge 1
    set version $argv[1]
end

set -l out_dir "data/external/spire-codex/$version/eng"
mkdir -p "$out_dir"

set -l files     acts.json     afflictions.json     ascensions.json     cards.json     characters.json     enchantments.json     encounters.json     epochs.json     events.json     glossary.json     intents.json     keywords.json     modifiers.json     monsters.json     orbs.json     potions.json     powers.json     relics.json     rest_site_options.json

for file in $files
    set -l url "https://raw.githubusercontent.com/ptrlrd/spire-codex/main/data-beta/$version/eng/$file"
    echo "Fetching $file"
    curl --fail --location --silent --show-error "$url" --output "$out_dir/$file"
    or exit 1
end

printf '%s\n'     "Local research cache only."     "Source: ptrlrd/spire-codex data-beta/$version/eng"     "Game data is © Mega Crit Games; do not commit or redistribute this cache."     > "$out_dir/SOURCE.txt"

echo "Synced Spire Codex $version to $out_dir"
