#!/usr/bin/env python3
"""Mail API smoke (happy + bad). Windows-friendly: python scripts/smoke_test.py"""
from __future__ import annotations

import base64
import hashlib
import hmac
import json
import os
import sys
import time
import urllib.error
import urllib.request

BASE = os.environ.get("BASE", "http://localhost:12081")
ADMIN_KEY = os.environ.get("ADMIN_KEY", "dev-admin-key")
JWT_SECRET = os.environ.get("JWT_SECRET", "dev-jwt-secret-shared-with-mp")
USER_ID = os.environ.get("USER_ID", "sample-user-1")
PROJECT_ID = os.environ.get("PROJECT_ID", "default")

PASS = FAIL = 0


def b64url(data: bytes) -> str:
    return base64.urlsafe_b64encode(data).rstrip(b"=").decode("ascii")


def make_jwt(user_id: str, secret: str, ttl: int = 3600) -> str:
    now = int(time.time())
    header = b64url(b'{"alg":"HS256","typ":"JWT"}')
    payload = b64url(
        json.dumps({"sub": user_id, "iat": now, "exp": now + ttl}, separators=(",", ":")).encode()
    )
    sig = b64url(hmac.new(secret.encode(), f"{header}.{payload}".encode(), hashlib.sha256).digest())
    return f"{header}.{payload}.{sig}"


def req(method: str, url: str, body: dict | None = None, headers: dict | None = None):
    data = None
    hdr = dict(headers or {})
    if body is not None:
        data = json.dumps(body).encode()
        hdr.setdefault("Content-Type", "application/json")
    r = urllib.request.Request(url, data=data, headers=hdr, method=method)
    try:
        with urllib.request.urlopen(r, timeout=15) as resp:
            raw = resp.read().decode()
            return resp.status, raw
    except urllib.error.HTTPError as e:
        raw = e.read().decode()
        return e.code, raw


def expect(name: str, method: str, url: str, expected: int, body=None, headers=None):
    global PASS, FAIL
    code, raw = req(method, url, body, headers)
    if code == expected:
        if expected != 204 and raw:
            try:
                json.loads(raw)
            except json.JSONDecodeError:
                print(f"  FAIL  {name}  non-JSON body: {raw[:200]}")
                FAIL += 1
                return raw
        print(f"  PASS  {name}  ({code})")
        PASS += 1
    else:
        print(f"  FAIL  {name}  expected={expected} got={code} body={raw[:300]}")
        FAIL += 1
    return raw


def main():
    print(f"=== Mail smoke against {BASE} ===")
    jwt = make_jwt(USER_ID, JWT_SECRET)
    bad_jwt = make_jwt(USER_ID, "wrong-secret-xxxxx")

    print("--- health ---")
    expect("GET /health", "GET", f"{BASE}/health", 200)

    print("--- admin bad path ---")
    expect(
        "admin create no key -> 401",
        "POST",
        f"{BASE}/api/v1/admin/mails",
        401,
        {"projectId": PROJECT_ID, "title": "x", "content": "y", "targetUserIds": [USER_ID]},
    )
    expect(
        "admin create bad key -> 401",
        "POST",
        f"{BASE}/api/v1/admin/mails",
        401,
        {"projectId": PROJECT_ID, "title": "x", "content": "y", "targetUserIds": [USER_ID]},
        {"X-Admin-Api-Key": "wrong-key"},
    )
    expect(
        "admin create missing fields -> 400",
        "POST",
        f"{BASE}/api/v1/admin/mails",
        400,
        {"projectId": PROJECT_ID},
        {"X-Admin-Api-Key": ADMIN_KEY},
    )
    expect(
        "admin create no targets -> 400",
        "POST",
        f"{BASE}/api/v1/admin/mails",
        400,
        {"projectId": PROJECT_ID, "title": "t", "content": "c", "targetUserIds": []},
        {"X-Admin-Api-Key": ADMIN_KEY},
    )

    print("--- admin happy ---")
    raw = expect(
        "admin create OK -> 201",
        "POST",
        f"{BASE}/api/v1/admin/mails",
        201,
        {
            "projectId": PROJECT_ID,
            "title": f"smoke {int(time.time())}",
            "content": "smoke body",
            "attachments": [{"itemId": "gem", "count": 10}],
            "targetUserIds": [USER_ID],
            "senderName": "Smoke",
        },
        {"X-Admin-Api-Key": ADMIN_KEY},
    )
    mail_id = ""
    try:
        mail_id = json.loads(raw).get("id", "")
    except Exception:
        pass

    expect(
        "admin list -> 200",
        "GET",
        f"{BASE}/api/v1/admin/mails?projectId={PROJECT_ID}",
        200,
        headers={"X-Admin-Api-Key": ADMIN_KEY},
    )
    if mail_id:
        expect(
            "admin get -> 200",
            "GET",
            f"{BASE}/api/v1/admin/mails/{mail_id}",
            200,
            headers={"X-Admin-Api-Key": ADMIN_KEY},
        )
    expect(
        "admin get missing -> 404",
        "GET",
        f"{BASE}/api/v1/admin/mails/00000000-0000-0000-0000-000000000000",
        404,
        headers={"X-Admin-Api-Key": ADMIN_KEY},
    )

    print("--- player bad path ---")
    expect("inbox no auth -> 401", "GET", f"{BASE}/api/v1/inbox?projectId={PROJECT_ID}", 401)
    expect(
        "inbox bad jwt -> 401",
        "GET",
        f"{BASE}/api/v1/inbox?projectId={PROJECT_ID}",
        401,
        headers={"Authorization": f"Bearer {bad_jwt}"},
    )
    missing = "00000000-0000-0000-0000-000000000000"
    auth = {"Authorization": f"Bearer {jwt}"}
    expect("read missing -> 404", "POST", f"{BASE}/api/v1/inbox/{missing}/read?projectId={PROJECT_ID}", 404, headers=auth)
    expect("claim missing -> 404", "POST", f"{BASE}/api/v1/inbox/{missing}/claim?projectId={PROJECT_ID}", 404, headers=auth)
    expect("delete missing -> 404", "DELETE", f"{BASE}/api/v1/inbox/{missing}?projectId={PROJECT_ID}", 404, headers=auth)

    print("--- player happy ---")
    raw = expect(
        "inbox list -> 200",
        "GET",
        f"{BASE}/api/v1/inbox?projectId={PROJECT_ID}&includeClaimed=true&page=1&pageSize=50",
        200,
        headers=auth,
    )
    user_mail_id = ""
    try:
        items = json.loads(raw).get("items") or []
        if items:
            user_mail_id = items[0]["id"]
    except Exception:
        pass

    if not user_mail_id:
        print("  FAIL  no userMailId from inbox")
        global FAIL
        FAIL += 1
    else:
        expect("mark read -> 200", "POST", f"{BASE}/api/v1/inbox/{user_mail_id}/read?projectId={PROJECT_ID}", 200, headers=auth)
        expect("claim -> 200", "POST", f"{BASE}/api/v1/inbox/{user_mail_id}/claim?projectId={PROJECT_ID}", 200, headers=auth)
        expect("claim again -> 200", "POST", f"{BASE}/api/v1/inbox/{user_mail_id}/claim?projectId={PROJECT_ID}", 200, headers=auth)
        expect("delete -> 204", "DELETE", f"{BASE}/api/v1/inbox/{user_mail_id}?projectId={PROJECT_ID}", 204, headers=auth)
        expect("delete again -> 404", "DELETE", f"{BASE}/api/v1/inbox/{user_mail_id}?projectId={PROJECT_ID}", 404, headers=auth)

    print(f"=== summary: PASS={PASS} FAIL={FAIL} ===")
    sys.exit(1 if FAIL else 0)


if __name__ == "__main__":
    main()
