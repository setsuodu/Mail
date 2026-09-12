#!/usr/bin/env bash
# Mail API smoke: happy path + bad path (AOT-safe error JSON).
# Windows: run in Git Bash from repo (path with [brackets] is fine).
#   cd server && bash scripts/smoke-test.sh
set -euo pipefail

# --- resolve dirs (Git Bash: pwd -W → D:/... for Windows python) ---
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

win_path() {
  # Convert MSYS path to Windows path for native python.exe
  if command -v cygpath >/dev/null 2>&1; then
    cygpath -w "$1"
  else
    # fallback: pwd -W style
    local d f
    d="$(cd "$(dirname "$1")" && pwd -W 2>/dev/null || pwd)"
    f="$(basename "$1")"
    echo "$d/$f" | sed 's|/|\\|g'
  fi
}

if command -v python >/dev/null 2>&1; then
  PYTHON=python
elif command -v python3 >/dev/null 2>&1; then
  PYTHON=python3
else
  echo "Python not found. Install Python 3 or disable Windows Store python alias."
  exit 1
fi

GEN_JWT="$(win_path "$SCRIPT_DIR/gen_jwt.py")"

BASE="${BASE:-http://localhost:12081}"
ADMIN_KEY="${ADMIN_KEY:-dev-admin-key}"
JWT_SECRET="${JWT_SECRET:-dev-jwt-secret-shared-with-mp}"
USER_ID="${USER_ID:-sample-user-1}"
PROJECT_ID="${PROJECT_ID:-default}"

PASS=0
FAIL=0
DETAIL=()

green() { printf '\033[32m%s\033[0m\n' "$*"; }
red()   { printf '\033[31m%s\033[0m\n' "$*"; }
info()  { printf '\033[36m%s\033[0m\n' "$*"; }

py() {
  # run python -c without path issues
  "$PYTHON" -c "$1" "${@:2}"
}

expect() {
  local name="$1" method="$2" url="$3" expected="$4"
  shift 4
  local body=""
  local -a hdr=()
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --body) body="$2"; shift 2 ;;
      --header) hdr+=(-H "$2"); shift 2 ;;
      *) shift ;;
    esac
  done

  local tmp
  tmp="$(mktemp)"
  local code
  if [[ -n "$body" ]]; then
    code="$(curl -sS -o "$tmp" -w '%{http_code}' -X "$method" "$url" \
      -H 'Content-Type: application/json' "${hdr[@]}" --data "$body" || true)"
  else
    code="$(curl -sS -o "$tmp" -w '%{http_code}' -X "$method" "$url" "${hdr[@]}" || true)"
  fi
  LAST_BODY="$(cat "$tmp")"
  rm -f "$tmp"

  if [[ "$code" == "$expected" ]]; then
    green "  PASS  $name  ($code)"
    PASS=$((PASS + 1))
    if [[ "$expected" != "204" && -n "$LAST_BODY" ]]; then
      if ! "$PYTHON" -c "import json,sys; json.loads(sys.argv[1])" "$LAST_BODY" 2>/dev/null; then
        red "  FAIL  $name  body is not JSON (AOT risk?): ${LAST_BODY:0:200}"
        FAIL=$((FAIL + 1))
        PASS=$((PASS - 1))
        DETAIL+=("$name: non-JSON body")
      fi
    fi
  else
    red "  FAIL  $name  expected=$expected got=$code body=${LAST_BODY:0:300}"
    FAIL=$((FAIL + 1))
    DETAIL+=("$name: HTTP $code")
  fi
}

info "=== Mail smoke against $BASE ==="
info "gen_jwt: $GEN_JWT"

JWT="$("$PYTHON" "$GEN_JWT" "$USER_ID" "$JWT_SECRET")"
BAD_JWT="$("$PYTHON" "$GEN_JWT" "$USER_ID" "wrong-secret-xxxxx")"

info "--- health ---"
expect "GET /health" GET "$BASE/health" 200

info "--- admin bad path ---"
expect "admin create no key -> 401" POST "$BASE/api/v1/admin/mails" 401 \
  --body "{\"projectId\":\"$PROJECT_ID\",\"title\":\"x\",\"content\":\"y\",\"targetUserIds\":[\"$USER_ID\"]}"

expect "admin create bad key -> 401" POST "$BASE/api/v1/admin/mails" 401 \
  --header "X-Admin-Api-Key: wrong-key" \
  --body "{\"projectId\":\"$PROJECT_ID\",\"title\":\"x\",\"content\":\"y\",\"targetUserIds\":[\"$USER_ID\"]}"

expect "admin create missing fields -> 400" POST "$BASE/api/v1/admin/mails" 400 \
  --header "X-Admin-Api-Key: $ADMIN_KEY" \
  --body "{\"projectId\":\"$PROJECT_ID\"}"

expect "admin create no targets -> 400" POST "$BASE/api/v1/admin/mails" 400 \
  --header "X-Admin-Api-Key: $ADMIN_KEY" \
  --body "{\"projectId\":\"$PROJECT_ID\",\"title\":\"t\",\"content\":\"c\",\"targetUserIds\":[]}"

info "--- admin happy ---"
TITLE="smoke $(date -u +%H%M%S 2>/dev/null || echo now)"
CREATE_BODY="{\"projectId\":\"$PROJECT_ID\",\"title\":\"$TITLE\",\"content\":\"smoke body\",\"attachments\":[{\"itemId\":\"gem\",\"count\":10}],\"targetUserIds\":[\"$USER_ID\"],\"senderName\":\"Smoke\"}"

expect "admin create OK -> 201" POST "$BASE/api/v1/admin/mails" 201 \
  --header "X-Admin-Api-Key: $ADMIN_KEY" \
  --body "$CREATE_BODY"
MAIL_ID="$("$PYTHON" -c "import json,sys; print(json.loads(sys.argv[1]).get('id',''))" "$LAST_BODY" 2>/dev/null || true)"

expect "admin list -> 200" GET "$BASE/api/v1/admin/mails?projectId=$PROJECT_ID" 200 \
  --header "X-Admin-Api-Key: $ADMIN_KEY"

if [[ -n "$MAIL_ID" ]]; then
  expect "admin get -> 200" GET "$BASE/api/v1/admin/mails/$MAIL_ID" 200 \
    --header "X-Admin-Api-Key: $ADMIN_KEY"
fi
expect "admin get missing -> 404" GET "$BASE/api/v1/admin/mails/00000000-0000-0000-0000-000000000000" 404 \
  --header "X-Admin-Api-Key: $ADMIN_KEY"

info "--- player bad path ---"
expect "inbox no auth -> 401" GET "$BASE/api/v1/inbox?projectId=$PROJECT_ID" 401

expect "inbox bad jwt -> 401" GET "$BASE/api/v1/inbox?projectId=$PROJECT_ID" 401 \
  --header "Authorization: Bearer $BAD_JWT"

expect "read missing mail -> 404" POST "$BASE/api/v1/inbox/00000000-0000-0000-0000-000000000000/read?projectId=$PROJECT_ID" 404 \
  --header "Authorization: Bearer $JWT"

expect "claim missing mail -> 404" POST "$BASE/api/v1/inbox/00000000-0000-0000-0000-000000000000/claim?projectId=$PROJECT_ID" 404 \
  --header "Authorization: Bearer $JWT"

expect "delete missing mail -> 404" DELETE "$BASE/api/v1/inbox/00000000-0000-0000-0000-000000000000?projectId=$PROJECT_ID" 404 \
  --header "Authorization: Bearer $JWT"

info "--- player happy ---"
expect "inbox list -> 200" GET "$BASE/api/v1/inbox?projectId=$PROJECT_ID&includeClaimed=true&page=1&pageSize=50" 200 \
  --header "Authorization: Bearer $JWT"

USER_MAIL_ID="$("$PYTHON" -c "
import json,sys
d=json.loads(sys.argv[1])
items=d.get('items') or []
print(items[0]['id'] if items else '')
" "$LAST_BODY" 2>/dev/null || true)"

if [[ -z "$USER_MAIL_ID" ]]; then
  red "  FAIL  could not resolve userMailId from inbox (seed may have failed)"
  FAIL=$((FAIL + 1))
  DETAIL+=("no userMailId")
else
  expect "mark read -> 200" POST "$BASE/api/v1/inbox/$USER_MAIL_ID/read?projectId=$PROJECT_ID" 200 \
    --header "Authorization: Bearer $JWT"

  expect "claim -> 200" POST "$BASE/api/v1/inbox/$USER_MAIL_ID/claim?projectId=$PROJECT_ID" 200 \
    --header "Authorization: Bearer $JWT"

  expect "claim again -> 200" POST "$BASE/api/v1/inbox/$USER_MAIL_ID/claim?projectId=$PROJECT_ID" 200 \
    --header "Authorization: Bearer $JWT"

  expect "delete -> 204" DELETE "$BASE/api/v1/inbox/$USER_MAIL_ID?projectId=$PROJECT_ID" 204 \
    --header "Authorization: Bearer $JWT"

  expect "delete again -> 404" DELETE "$BASE/api/v1/inbox/$USER_MAIL_ID?projectId=$PROJECT_ID" 404 \
    --header "Authorization: Bearer $JWT"
fi

info "=== summary: PASS=$PASS FAIL=$FAIL ==="
if [[ "$FAIL" -gt 0 ]]; then
  red "Failures:"
  for d in "${DETAIL[@]}"; do red "  - $d"; done
  exit 1
fi
green "All smoke checks passed."
