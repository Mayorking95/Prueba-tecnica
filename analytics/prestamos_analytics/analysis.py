"""Tablas de resultados: descriptivo, rentabilidad por tramo, niveles de riesgo y política de corte."""

from __future__ import annotations

import numpy as np
import pandas as pd

from . import config


def by_band_descriptive(df: pd.DataFrame) -> pd.DataFrame:
    """Aprobación y tiempos por tramo de monto. Solo cuenta solicitudes ya resueltas."""
    d = df[df["status"].isin(["approved", "rejected"])].copy()
    d["hours"] = (d["resolved_at"] - d["created_at"]).dt.total_seconds() / 3600
    g = d.groupby("band", observed=False)
    out = g.agg(
        solicitudes=("id", "size"),
        monto_promedio=("amount", "mean"),
        plazo_promedio=("term_months", "mean"),
        tasa_aprobacion=("status", lambda s: (s == "approved").mean()),
        horas_resolucion_mediana=("hours", "median"),
    )
    return out.reset_index()


def economics_by_band(approved: pd.DataFrame) -> pd.DataFrame:
    """Rentabilidad realizada por tramo (requiere desenlace conocido)."""
    g = approved.groupby("band", observed=False)
    out = g.agg(
        prestamos=("id", "size"),
        capital=("amount", "sum"),
        interes_programado=("scheduled_interest", "sum"),
        interes_cobrado=("interest_collected", "sum"),
        perdidas=("loss", "sum"),
        resultado_neto=("net_result", "sum"),
        tasa_incumplimiento=("defaulted", "mean"),
    )
    out["retorno_sobre_capital"] = out["resultado_neto"] / out["capital"]
    return out.reset_index()


def economics_by_tier(scored: pd.DataFrame) -> pd.DataFrame:
    g = scored.groupby("risk_tier", observed=False)
    out = g.agg(
        prestamos=("id", "size"),
        pd_promedio=("pd_hat", "mean"),
        tasa_incumplimiento=("defaulted", "mean"),
        capital=("amount", "sum"),
        interes_cobrado=("interest_collected", "sum"),
        perdidas=("loss", "sum"),
        resultado_neto=("net_result", "sum"),
    )
    out["retorno_sobre_capital"] = out["resultado_neto"] / out["capital"]
    return out.reset_index()


def cutoff_policy(scored: pd.DataFrame, cutoffs: np.ndarray | None = None) -> pd.DataFrame:
    """Resultado neto si SOLO se aprobaran préstamos con PD <= corte.

    Se evalúa sobre préstamos aprobados de la muestra de prueba: es una simulación sobre lo ya aprobado,
    no mide qué habría pasado con los rechazados (sesgo de selección).
    """
    if cutoffs is None:
        cutoffs = np.append(np.round(np.arange(0.04, 0.401, 0.01), 2), 1.0)
    total_capital = scored["amount"].sum()
    rows = []
    for c in cutoffs:
        keep = scored[scored["pd_hat"] <= c]
        rows.append(
            {
                "corte_pd": c,
                "prestamos": len(keep),
                "porcentaje_aprobado": len(keep) / len(scored),
                "capital": keep["amount"].sum(),
                "resultado_neto": keep["net_result"].sum(),
                "retorno_sobre_capital": keep["net_result"].sum() / keep["amount"].sum() if len(keep) else np.nan,
                "capital_colocado_pct": keep["amount"].sum() / total_capital,
            }
        )
    return pd.DataFrame(rows)


def band_rates_table() -> pd.DataFrame:
    return pd.DataFrame(
        {"tramo": config.BAND_LABELS, "tasa_efectiva_anual_supuesta": [config.ANNUAL_RATE_BY_BAND[b] for b in config.BAND_LABELS]}
    )
