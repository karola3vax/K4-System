#!/usr/bin/env bash
# Checks that a Release build of K4-System has every file the server package needs.
set -euo pipefail
P=K4-System/src/bin/K4-System
for f in \
  plugins/K4-System/K4-System.dll plugins/K4-System/K4-System.deps.json \
  plugins/K4-System/Dapper.dll plugins/K4-System/MySqlConnector.dll \
  plugins/K4-System/Microsoft.Data.Sqlite.dll plugins/K4-System/SQLitePCLRaw.core.dll plugins/K4-System/SQLitePCLRaw.provider.e_sqlite3.dll \
  plugins/K4-System/runtimes/linux-x64/native/libe_sqlite3.so plugins/K4-System/runtimes/win-x64/native/e_sqlite3.dll \
  plugins/K4-System/MaxMind.GeoIP2.dll plugins/K4-System/MaxMind.Db.dll \
  shared/K4-SharedApi/K4-SharedApi.dll; do
  test -f "$P/$f" || { echo "Missing $f" >&2; exit 1; }
done

shipped=$(find K4-System/src/lang -maxdepth 1 -type f -name '*.json' | wc -l)
packaged=$(find "$P/plugins/K4-System/lang" -maxdepth 1 -type f -name '*.json' | wc -l)
test "$packaged" -eq "$shipped" || { echo "The package has $packaged language files, the source has $shipped." >&2; exit 1; }

if find "$P" -type f | grep -Eq 'MMRanks|FakeRanks'; then
  echo "Release package contains an old companion plugin." >&2
  exit 1
fi
echo "Package complete: $packaged languages."
