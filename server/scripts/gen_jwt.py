#!/usr/bin/env python3
"""HS256 JWT aligned with MP SimpleJwt / SampleJwt (no kid)."""
import base64, hashlib, hmac, json, sys, time

def b64url(data: bytes) -> str:
    return base64.urlsafe_b64encode(data).rstrip(b"=").decode("ascii")

def main():
    user_id = sys.argv[1] if len(sys.argv) > 1 else "sample-user-1"
    secret = sys.argv[2] if len(sys.argv) > 2 else "dev-jwt-secret-shared-with-mp"
    ttl = int(sys.argv[3]) if len(sys.argv) > 3 else 3600
    now = int(time.time())
    header = b64url(b'{"alg":"HS256","typ":"JWT"}')
    payload = b64url(json.dumps(
        {"sub": user_id, "iat": now, "exp": now + ttl},
        separators=(",", ":")).encode())
    sig_input = f"{header}.{payload}".encode()
    sig = b64url(hmac.new(secret.encode(), sig_input, hashlib.sha256).digest())
    print(f"{header}.{payload}.{sig}")

if __name__ == "__main__":
    main()
