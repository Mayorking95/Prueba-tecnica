"""Datos SINTÉTICOS de préstamos, con la misma forma que la tabla real `loan_requests`.

Por qué sintéticos: la app tiene pocos usuarios y no registra pagos, así que no existe una variable
de incumplimiento real. Este generador simula un portafolio con supuestos explícitos y semilla fija
(reproducible). Los resultados NO son hallazgos sobre clientes reales: sirven para demostrar el
método y dejarlo listo para datos reales.

Supuestos de comportamiento (ver `generate`):
- Cada usuario tiene una "solvencia" latente que el administrador NO ve, pero que influye en el
  incumplimiento. Esto es lo que hace el problema realista: el historial es solo una señal parcial.
- La probabilidad de incumplimiento sube con el monto, el plazo y los incumplimientos previos, y baja
  con el historial de pagos.
- El administrador rechaza más los montos altos y a quienes ya incumplieron antes, con ruido.
- Solo los préstamos aprobados tienen desenlace (pagó / incumplió); los rechazados no se observan.
"""

from __future__ import annotations

from datetime import datetime, timedelta

import numpy as np
import pandas as pd

TERMS = np.array([6, 12, 18, 24, 36, 48, 60])


def _sigmoid(x: float) -> float:
    return 1.0 / (1.0 + np.exp(-x))


def generate(n_users: int = 1500, seed: int = 42) -> pd.DataFrame:
    rng = np.random.default_rng(seed)
    start = datetime(2025, 1, 1)
    rows: list[dict] = []
    loan_id = 0

    for user_id in range(1, n_users + 1):
        solvency = rng.normal()  # latente: mayor = mejor pagador (no observable por el admin)
        base_amount = np.exp(rng.normal(np.log(2_500_000), 0.9))
        n_loans = 1 + rng.poisson(rng.gamma(2.0, 1.2))
        t = start + timedelta(days=int(rng.integers(0, 540)))

        prior_defaults = 0
        prior_paid = 0
        prior_loans = 0
        amounts: list[float] = []

        for _ in range(n_loans):
            amount = float(np.clip(np.round(base_amount * np.exp(rng.normal(0, 0.4)), -4), 100_000, 50_000_000))

            target = np.clip(6 + amount / 1_200_000, 6, 60)
            w = np.exp(-(((TERMS - target) / 10.0) ** 2))
            term = int(rng.choice(TERMS, p=w / w.sum()))

            ratio = amount / np.mean(amounts) if amounts else 1.0
            ln_a = np.log(amount / 2_500_000)

            p_approve = _sigmoid(1.6 - 0.6 * ln_a - 1.2 * prior_defaults - 0.4 * (ratio > 3) + 0.2 * min(prior_paid, 3))
            approved = rng.random() < p_approve

            created = t
            resolved = created + timedelta(hours=float(rng.exponential(18) * max(0.5, 1 + 0.3 * ln_a) + 1))

            defaulted = np.nan
            default_month = np.nan
            if approved:
                logit = (
                    -2.3
                    + 0.5 * ln_a
                    + 0.02 * (term - 24)
                    + 1.0 * prior_defaults
                    + 0.35 * (prior_loans == 0)
                    + 0.3 * (ratio > 2)
                    - 0.5 * solvency
                )
                if rng.random() < _sigmoid(logit):
                    defaulted, default_month = 1.0, float(rng.integers(1, term))
                    prior_defaults += 1
                else:
                    defaulted = 0.0
                    prior_paid += 1

            loan_id += 1
            rows.append(
                {
                    "id": loan_id,
                    "user_id": user_id,
                    "user_name": f"Usuario {user_id}",
                    "amount": amount,
                    "term_months": term,
                    "status": "approved" if approved else "rejected",
                    "created_at": created,
                    "resolved_at": resolved,
                    "defaulted": defaulted,
                    "default_month": default_month,
                }
            )

            amounts.append(amount)
            prior_loans += 1
            t = resolved + timedelta(days=int(rng.integers(20, 120)))

    df = pd.DataFrame(rows)
    df["created_at"] = pd.to_datetime(df["created_at"], utc=True)
    df["resolved_at"] = pd.to_datetime(df["resolved_at"], utc=True)
    return df
