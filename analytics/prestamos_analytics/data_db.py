"""Lectura de las solicitudes REALES desde la base PostgreSQL de la app."""

from __future__ import annotations

import os

import pandas as pd
import psycopg

ENV_VAR = "PRESTAMOS_CONN"

_NPGSQL_KEYS = {"host": "host", "port": "port", "database": "dbname", "username": "user", "password": "password"}


def to_conninfo(raw: str) -> str:
    """Acepta una URL postgresql://… o la cadena estilo Npgsql de `user-secrets` (Host=…;Port=…)."""
    raw = raw.strip()
    if raw.startswith(("postgres://", "postgresql://")):
        return raw
    parts = {}
    for chunk in raw.split(";"):
        if "=" in chunk:
            key, value = chunk.split("=", 1)
            mapped = _NPGSQL_KEYS.get(key.strip().lower())
            if mapped:
                parts[mapped] = value.strip()
    return " ".join(f"{k}={v}" for k, v in parts.items())


QUERY = """
SELECT l."Id"::text          AS id,
       l."UserId"::text      AS user_id,
       u."FullName"          AS user_name,
       l."Amount"::float8    AS amount,
       l."TermMonths"        AS term_months,
       lower(l."Status")     AS status,
       l."CreatedAt"         AS created_at,
       l."ResolvedAt"        AS resolved_at
FROM loan_requests l
JOIN users u ON u."Id" = l."UserId"
ORDER BY l."CreatedAt"
"""


def load_loans(conn: str | None = None) -> pd.DataFrame:
    """Devuelve las solicitudes reales. La conexión sale del argumento o de la variable PRESTAMOS_CONN."""
    conn = conn or os.environ.get(ENV_VAR)
    if not conn:
        raise RuntimeError(f"Define la variable de entorno {ENV_VAR} con la cadena de conexión.")
    with psycopg.connect(to_conninfo(conn)) as cx, cx.cursor() as cur:
        cur.execute(QUERY)
        columns = [c.name for c in cur.description]
        df = pd.DataFrame(cur.fetchall(), columns=columns)
    df["created_at"] = pd.to_datetime(df["created_at"], utc=True)
    df["resolved_at"] = pd.to_datetime(df["resolved_at"], utc=True)
    return df
