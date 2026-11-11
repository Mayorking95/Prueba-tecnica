"""Variables derivadas del historial de cada usuario, calculadas SOLO con información previa."""

from __future__ import annotations

import numpy as np
import pandas as pd


def add_history_features(df: pd.DataFrame) -> pd.DataFrame:
    """Agrega historial previo por usuario (en orden cronológico de solicitud).

    Un préstamo nunca "ve" su propio resultado ni el de solicitudes posteriores.
    `defaulted` puede no existir (datos reales de la app): en ese caso prior_defaults = 0.
    """
    out = df.sort_values(["user_id", "created_at"]).reset_index(drop=True).copy()
    g = out.groupby("user_id", sort=False)

    out["prior_loans"] = g.cumcount()

    rejected = (out["status"] == "rejected").astype(int)
    out["prior_rejections"] = rejected.groupby(out["user_id"]).cumsum() - rejected

    if "defaulted" in out.columns:
        d = out["defaulted"].fillna(0).astype(int)
        out["prior_defaults"] = d.groupby(out["user_id"]).cumsum() - d
    else:
        out["prior_defaults"] = 0

    prev_sum = out.groupby("user_id", sort=False)["amount"].cumsum() - out["amount"]
    prev_avg = prev_sum / out["prior_loans"].replace(0, np.nan)
    out["amount_ratio"] = (out["amount"] / prev_avg).fillna(1.0).clip(upper=10.0)

    out["log_amount"] = np.log(out["amount"])
    return out
