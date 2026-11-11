"""Supuestos del análisis. TODO lo que no sale de la app vive aquí, a la vista.

La app solo guarda usuario, monto, plazo, estado y fechas. La tasa de interés, el
incumplimiento y la recuperación NO existen en la app: son supuestos de este modelo.
"""

from __future__ import annotations

import numpy as np

BAND_EDGES = [0, 1_000_000, 5_000_000, 20_000_000, np.inf]
BAND_LABELS = ["<1M", "1M-5M", "5M-20M", ">20M"]

ANNUAL_RATE_BY_BAND = {"<1M": 0.30, "1M-5M": 0.26, "5M-20M": 0.22, ">20M": 0.18}

RECOVERY_RATE = 0.30

FEATURES = [
    "log_amount",
    "term_months",
    "prior_loans",
    "prior_defaults",
    "prior_rejections",
    "amount_ratio",
]

TIER_EDGES = [0.0, 0.08, 0.16, 1.0]
TIER_LABELS = ["Bajo", "Medio", "Alto"]

COLORS = {
    "blue": "#2a78d6",
    "orange": "#eb6834",
    "aqua": "#1baf7a",
    "surface": "#fcfcfb",
    "ink": "#0b0b0b",
    "ink2": "#52514e",
    "grid": "#e6e5e1",
}
