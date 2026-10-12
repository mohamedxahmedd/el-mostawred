#!/usr/bin/env bash
# Regenerates deploy/sql/migrate-idempotent.sql — safe to run against an empty or partly migrated database, and safe to re-run.
# The SET header is needed because some clients (e.g. sqlcmd) default QUOTED_IDENTIFIER to OFF, which filtered indexes reject.
set -euo pipefail
cd "$(dirname "$0")/.."
out=deploy/sql/migrate-idempotent.sql
tmp=$(mktemp)
dotnet tool restore >/dev/null
dotnet ef migrations script --idempotent -p src/SamyrTechWay.SpinWin -o "$tmp"
{
  printf 'SET ANSI_NULLS ON;\nSET QUOTED_IDENTIFIER ON;\nSET ANSI_PADDING ON;\nSET ANSI_WARNINGS ON;\nSET ARITHABORT ON;\nSET CONCAT_NULL_YIELDS_NULL ON;\nSET NUMERIC_ROUNDABORT OFF;\nGO\n\n'
  sed '1s/^\xEF\xBB\xBF//' "$tmp"
} > "$out"
rm -f "$tmp"
echo "Wrote $out"
