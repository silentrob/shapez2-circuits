#!/usr/bin/env bash
# Uploads the built mod to the Steam Workshop, as the official sample mods do (shapez 2 modding documentation,
# "Publishing"). Run through the project's SteamPublish target, which stages the mod's files and passes their
# folder:
#
#   STEAM_USERNAME=<your Steam login> dotnet msbuild Circuits.csproj -t:SteamPublish
#
# Needs steamcmd on the PATH; it asks for the password (and Steam Guard code) itself. base.vdf holds the Workshop
# item: its "publishedfileid" is 0 until the first upload creates the item, and is then written back here, so
# commit it. "visibility" is 0, public (2 is private).

set -e

if [ -z "$STEAM_USERNAME" ]; then
    echo "Set STEAM_USERNAME to the Steam account that publishes the mod." >&2
    exit 1
fi

CONTENT_PATH=$1

# The working directory as a Windows path (C:\...), for steamcmd.
CURRENT_DIR=$(cygpath -w "$PWD")
PREVIEW_IMG=$CURRENT_DIR\\Steam\\preview.png

# The .vdf wants backslashes doubled.
CONTENT_PATH="${CONTENT_PATH//\\/\\\\}"
PREVIEW_IMG="${PREVIEW_IMG//\//\\}"
PREVIEW_IMG="${PREVIEW_IMG//\\/\\\\}"

echo "CONTENT_PATH: $CONTENT_PATH"
echo "PREVIEW_IMG: $PREVIEW_IMG"
export CONTENT_PATH PREVIEW_IMG

# A temporary .vdf with the absolute paths filled in.
envsubst < Steam\\base.vdf > Steam\\base.tmp.vdf
cat Steam\\base.tmp.vdf

steamcmd +login "$STEAM_USERNAME" +workshop_build_item "$CURRENT_DIR\\Steam\\base.tmp.vdf" +quit

# steamcmd writes the item's id into the temporary .vdf; keep it in base.vdf so later uploads update the same item.
FILE_ID=$(grep '"publishedfileid"' Steam\\base.tmp.vdf | sed 's/.*"publishedfileid"[ \t]*"\([0-9]\+\)".*/\1/')
echo "Published file id: $FILE_ID"
sed -i 's/\("publishedfileid"[ \t]*"\)[0-9]\+"/\1'"$FILE_ID"'"/' Steam\\base.vdf

rm Steam\\base.tmp.vdf
