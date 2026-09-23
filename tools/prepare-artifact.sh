#!/usr/bin/env bash
# Builds the browser client in the form a claude.ai Artifact publishes.
#
#   tools/prepare-artifact.sh <out-dir>
#
# Writes <out-dir>/artifact.html, the page, and <out-dir>/files.json, the map of every
# other file to publish beside it. Publishing them is done from a Claude Code session
# with the Artifact tool; docs/building.md has the call.
#
# Two things differ from a static host. The Artifact service supplies the document
# around the page, so the page is the title, icon, styles and body only; publishing
# a whole document nests one inside the other. And there is no <base href>: the page
# is served beside its framework/ folder, so its own address is the right base.
set -euo pipefail

out=${1:?usage: prepare-artifact.sh <out-dir>}
here=$(cd "$(dirname "$0")/.." && pwd)

rm -rf "$out" "$out.publish"
dotnet publish "$here/WebClient/WebClient.csproj" -c Release -o "$out.publish" >/dev/null
cp -r "$out.publish/wwwroot" "$out"
rm -rf "$out.publish"

"$here/tools/postprocess-publish.sh" "$out" / >/dev/null
rm -f "$out/.nojekyll"

python3 - "$out" <<'PY'
import json, os, re, sys

root = sys.argv[1]
page = open(os.path.join(root, 'index.html')).read()

title = re.search(r'<title>.*?</title>', page, re.S).group(0)
icons = ''.join(re.findall(r'<link rel="icon"[^>]*>', page))
styles = ''.join(re.findall(r'<style>.*?</style>', page, re.S))
body = re.search(r'<body>(.*)</body>', page, re.S).group(1).strip()
open(os.path.join(root, 'artifact.html'), 'w').write('\n'.join([title, icons, styles, body]) + '\n')

# The service infers most types from the extension, but not WebAssembly's.
files = {}
for folder, _, names in os.walk(root):
    for name in names:
        path = os.path.relpath(os.path.join(folder, name), root).replace(os.sep, '/')
        if path in ('index.html', 'artifact.html', 'files.json'):
            continue
        files[path] = {'from': path, 'contentType': 'application/wasm'} if path.endswith('.wasm') else path

json.dump(dict(sorted(files.items())), open(os.path.join(root, 'files.json'), 'w'))
print(f'{root}/artifact.html: {len(body)} bytes of body; {len(files)} files beside it')
PY
