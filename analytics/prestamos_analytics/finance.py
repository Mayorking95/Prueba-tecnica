"""Matemática financiera: sistema de amortización francés (cuota fija)."""

from __future__ import annotations

import numpy as np
import pandas as pd

from . import config


def band_for(amount: pd.Series) -> pd.Series:
    """Tramo de monto de cada préstamo (categoría ordenada)."""
    return pd.cut(amount, bins=config.BAND_EDGES, labels=config.BAND_LABELS, right=False)


def monthly_rate(annual_rate):
    """Tasa mensual equivalente a una tasa efectiva anual."""
    return (1.0 + np.asarray(annual_rate, dtype=float)) ** (1.0 / 12.0) - 1.0


def installment(principal, annual_rate, months):
    """Cuota mensual fija."""
    p = np.asarray(principal, dtype=float)
    n = np.asarray(months, dtype=float)
    r = monthly_rate(annual_rate)
    return p * r / (1.0 - (1.0 + r) ** (-n))


def total_interest(principal, annual_rate, months):
    """Interés total programado si el préstamo se paga completo."""
    return installment(principal, annual_rate, months) * np.asarray(months, dtype=float) - np.asarray(
        principal, dtype=float
    )


def balance_after(principal, annual_rate, months, paid):
    """Saldo de capital pendiente tras pagar `paid` cuotas."""
    p = np.asarray(principal, dtype=float)
    k = np.asarray(paid, dtype=float)
    r = monthly_rate(annual_rate)
    pmt = installment(principal, annual_rate, months)
    return p * (1.0 + r) ** k - pmt * ((1.0 + r) ** k - 1.0) / r


def interest_collected(principal, annual_rate, months, paid):
    """Interés cobrado en las primeras `paid` cuotas."""
    p = np.asarray(principal, dtype=float)
    pmt = installment(principal, annual_rate, months)
    capital_repaid = p - balance_after(principal, annual_rate, months, paid)
    return pmt * np.asarray(paid, dtype=float) - capital_repaid


def add_economics(df: pd.DataFrame) -> pd.DataFrame:
    """Agrega tramo, tasa supuesta, cuota e interés programado."""
    out = df.copy()
    out["band"] = band_for(out["amount"])
    out["annual_rate"] = out["band"].astype(str).map(config.ANNUAL_RATE_BY_BAND).astype(float)
    out["installment"] = installment(out["amount"], out["annual_rate"], out["term_months"])
    out["scheduled_interest"] = total_interest(out["amount"], out["annual_rate"], out["term_months"])
    return out


def add_realized_outcome(df: pd.DataFrame) -> pd.DataFrame:
    """Resultado realizado de préstamos aprobados con desenlace conocido.

    - Pagó todo: gana el interés programado.
    - Incumplió tras `default_month` cuotas: gana el interés cobrado hasta ahí y pierde
      (1 - recuperación) del saldo pendiente.
    Requiere columnas `defaulted` (0/1) y `default_month`.
    """
    out = df.copy()
    k = out["default_month"].fillna(0).to_numpy()
    defaulted = out["defaulted"].fillna(0).to_numpy().astype(bool)

    collected = np.where(
        defaulted,
        interest_collected(out["amount"], out["annual_rate"], out["term_months"], k),
        out["scheduled_interest"],
    )
    loss = np.where(
        defaulted,
        balance_after(out["amount"], out["annual_rate"], out["term_months"], k) * (1.0 - config.RECOVERY_RATE),
        0.0,
    )
    out["interest_collected"] = collected
    out["loss"] = loss
    out["net_result"] = collected - loss
    return out
