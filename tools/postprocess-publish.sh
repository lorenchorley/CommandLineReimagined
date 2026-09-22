#!/usr/bin/env bash
# Prepares a published Blazor WebAssembly folder for a static host.
#
#   tools/postprocess-publish.sh <wwwroot> [base-path]
#
# <wwwroot>    the publish output's wwwroot, edited in place
# [base-path]  the path the site is served under, e.g. /CommandLineReimagined
#              for a GitHub Pages project site. Omit or pass "/" for the root.
#
# Three things every static host needs, and one that only some do:
#   - the pre-compressed copies are dead weight: the loader fetches the plain files
#   - "_framework" is renamed to "framework", because some hosts reserve paths
#     beginning with an underscore, and Jekyll on GitHub Pages ignores them
#   - <base href> has to match the path the site is served under
#   - .nojekyll stops GitHub Pages running the files through Jekyll at all
set -euo pipefail

root=${1:?usage: postprocess-publish.sh <wwwroot> [base-path]}
base=${2:-/}

[ -d "$root" ] || { echo "no such folder: $root" >&2; exit 1; }
[ -f "$root/index.html" ] || { echo "no index.html in $root; is it the wwwroot?" >&2; exit 1; }

# A base path always ends in exactly one slash.
case "$base" in
  ''|'/') base='/' ;;
  */) ;;
  *) base="$base/" ;;
esac

cd "$root"

find . \( -name '*.gz' -o -name '*.br' \) -delete

if [ -d _framework ]; then
  mv _framework framework
fi

# The loader's own references, and any the page carries.
files=$(grep -rl '_framework' --include='*.js' --include='*.json' --include='*.html' . || true)
if [ -n "$files" ]; then
  echo "$files" | xargs sed -i 's/_framework/framework/g'
fi

sed -i "s|<base href=\"[^\"]*\" */>|<base href=\"$base\" />|" index.html

touch .nojekyll

bytes=$(du -sb . | cut -f1)
echo "base href: $(grep -o '<base href="[^"]*"' index.html)"
echo "payload:   $((bytes / 1048576)) MB across $(find . -type f | wc -l) files"
