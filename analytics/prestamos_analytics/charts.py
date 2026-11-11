"""Gráficos (PNG). Paleta validada, una sola escala por eje, marcas finas, rejilla recesiva."""

from __future__ import annotations

from pathlib import Path

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt 
import numpy as np  
import pandas as pd  
from matplotlib.patches import Patch 
from matplotlib.ticker import FuncFormatter, PercentFormatter

from .config import COLORS as C 

LABELS = {
    "log_amount": "Monto (log)",
    "term_months": "Plazo (meses)",
    "prior_loans": "Préstamos previos",
    "prior_defaults": "Incumplimientos previos",
    "prior_rejections": "Rechazos previos",
    "amount_ratio": "Monto / promedio previo",
}
millions = FuncFormatter(lambda v, _: f"{v / 1e6:,.0f}")


def _style(ax, title: str, subtitle: str | None = None):
    ax.set_facecolor(C["surface"])
    ax.figure.set_facecolor(C["surface"])
    for side in ("top", "right", "left"):
        ax.spines[side].set_visible(False)
    ax.spines["bottom"].set_color(C["grid"])
    ax.tick_params(colors=C["ink2"], length=0, labelsize=9)
    ax.grid(axis="y", color=C["grid"], linewidth=0.8)
    ax.set_axisbelow(True)
    ax.set_title(title, loc="left", fontsize=12, fontweight="bold", color=C["ink"], pad=24 if subtitle else 10)
    if subtitle:
        ax.text(0, 1.04, subtitle, transform=ax.transAxes, fontsize=9, color=C["ink2"])
    ax.xaxis.label.set_color(C["ink2"])
    ax.yaxis.label.set_color(C["ink2"])


def _save(fig, path: Path):
    fig.tight_layout()
    fig.savefig(path, dpi=150, facecolor=C["surface"])
    plt.close(fig)


def approval_by_band(desc: pd.DataFrame, path: Path):
    fig, ax = plt.subplots(figsize=(7, 4))
    x = np.arange(len(desc))
    ax.bar(x, desc["tasa_aprobacion"], width=0.5, color=C["blue"], edgecolor=C["surface"], linewidth=2)
    for xi, rate in zip(x, desc["tasa_aprobacion"]):
        ax.text(xi, rate + 0.015, f"{rate:.0%}", ha="center", color=C["ink"], fontsize=10, fontweight="bold")
    ax.set_xticks(x, [f"{b}\n(n={n:,})" for b, n in zip(desc["band"].astype(str), desc["solicitudes"])])
    ax.set_ylim(0, 1.05)
    ax.yaxis.set_major_formatter(PercentFormatter(1.0))
    ax.set_xlabel("Tramo de monto (COP)")
    _style(ax, "Tasa de aprobación por tramo de monto", "Solicitudes resueltas")
    _save(fig, path)


def economics_by_band(eco: pd.DataFrame, path: Path):
    fig, ax = plt.subplots(figsize=(8, 4.5))
    x = np.arange(len(eco))
    w = 0.26
    series = [
        ("Interés cobrado", eco["interes_cobrado"], C["blue"]),
        ("Pérdidas por incumplimiento", eco["perdidas"], C["orange"]),
        ("Resultado neto", eco["resultado_neto"], C["aqua"]),
    ]
    for i, (name, vals, color) in enumerate(series):
        ax.bar(x + (i - 1) * w, vals, width=w, color=color, edgecolor=C["surface"], linewidth=2, label=name)
    ax.set_xticks(x, eco["band"].astype(str))
    ax.yaxis.set_major_formatter(millions)
    ax.set_ylabel("Millones de COP")
    ax.set_xlabel("Tramo de monto (COP)")
    ax.legend(frameon=False, fontsize=9, loc="upper left", labelcolor=C["ink2"])
    _style(ax, "Interés, pérdidas y resultado neto por tramo", "Préstamos aprobados · datos sintéticos")
    _save(fig, path)


def default_by_band(eco: pd.DataFrame, path: Path):
    fig, ax = plt.subplots(figsize=(7, 4))
    x = np.arange(len(eco))
    ax.bar(x, eco["tasa_incumplimiento"], width=0.5, color=C["orange"], edgecolor=C["surface"], linewidth=2)
    for xi, rate in zip(x, eco["tasa_incumplimiento"]):
        ax.text(xi, rate + 0.004, f"{rate:.1%}", ha="center", color=C["ink"], fontsize=10, fontweight="bold")
    ax.set_xticks(x, eco["band"].astype(str))
    ax.yaxis.set_major_formatter(PercentFormatter(1.0, decimals=0))
    ax.set_xlabel("Tramo de monto (COP)")
    _style(ax, "Tasa de incumplimiento por tramo", "Préstamos aprobados · datos sintéticos")
    _save(fig, path)


def roc(roc_data: dict, metrics: pd.DataFrame, path: Path):
    fig, ax = plt.subplots(figsize=(5.5, 5.5))
    colors = [C["blue"], C["orange"]]
    for (name, (fpr, tpr)), color in zip(roc_data.items(), colors):
        auc = metrics.loc[metrics["modelo"] == name, "AUC"].iloc[0]
        ax.plot(fpr, tpr, color=color, linewidth=2, label=f"{name} (AUC {auc:.3f})")
    ax.plot([0, 1], [0, 1], color=C["ink2"], linewidth=1, linestyle="--", label="Sin poder predictivo")
    ax.set_xlabel("Falsos positivos")
    ax.set_ylabel("Verdaderos positivos")
    ax.legend(frameon=False, fontsize=9, loc="lower right", labelcolor=C["ink2"])
    ax.grid(axis="x", color=C["grid"], linewidth=0.8)
    _style(ax, "Curva ROC en datos de prueba", "Usuarios de prueba no vistos en el entrenamiento")
    _save(fig, path)


def calibration(cal: pd.DataFrame, path: Path):
    fig, ax = plt.subplots(figsize=(5.5, 5.5))
    top = float(max(cal["pred"].max(), cal["obs"].max())) * 1.15
    ax.plot([0, top], [0, top], color=C["ink2"], linewidth=1, linestyle="--", label="Calibración perfecta")
    ax.plot(cal["pred"], cal["obs"], color=C["blue"], linewidth=2, marker="o", markersize=7,
            markeredgecolor=C["surface"], markeredgewidth=2, label="Modelo (grupos de igual tamaño)")
    ax.xaxis.set_major_formatter(PercentFormatter(1.0, decimals=0))
    ax.yaxis.set_major_formatter(PercentFormatter(1.0, decimals=0))
    ax.set_xlabel("Probabilidad de incumplimiento predicha")
    ax.set_ylabel("Incumplimiento observado")
    ax.legend(frameon=False, fontsize=9, loc="upper left", labelcolor=C["ink2"])
    ax.grid(axis="x", color=C["grid"], linewidth=0.8)
    _style(ax, "Calibración: lo predicho vs. lo observado")
    _save(fig, path)


def coefficients(coefs: pd.Series, path: Path):
    fig, ax = plt.subplots(figsize=(7, 4))
    vals = coefs.sort_values()
    colors = [C["orange"] if v > 0 else C["aqua"] for v in vals]
    y = np.arange(len(vals))
    ax.barh(y, vals, height=0.55, color=colors, edgecolor=C["surface"], linewidth=2)
    ax.set_yticks(y, [LABELS.get(k, k) for k in vals.index])
    ax.axvline(0, color=C["ink2"], linewidth=1)
    ax.grid(axis="y", visible=False)
    ax.grid(axis="x", color=C["grid"], linewidth=0.8)
    ax.set_xlabel("Efecto sobre el riesgo (coeficiente estandarizado)")
    ax.legend(handles=[Patch(color=C["orange"], label="Aumenta el riesgo"), Patch(color=C["aqua"], label="Reduce el riesgo")],
              frameon=False, fontsize=9, loc="lower right", labelcolor=C["ink2"])
    _style(ax, "Qué pesa más en el riesgo", "Regresión logística")
    ax.grid(axis="y", visible=False)
    _save(fig, path)


def tiers(tier_df: pd.DataFrame, path: Path):
    fig, ax = plt.subplots(figsize=(7, 4))
    x = np.arange(len(tier_df))
    ax.bar(x, tier_df["tasa_incumplimiento"], width=0.5, color=C["orange"], edgecolor=C["surface"], linewidth=2)
    for xi, rate in zip(x, tier_df["tasa_incumplimiento"]):
        ax.text(xi, rate + 0.004, f"{rate:.1%}", ha="center", color=C["ink"], fontsize=10, fontweight="bold")
    ax.set_xticks(x, [f"{t}\n(n={n:,})" for t, n in zip(tier_df["risk_tier"].astype(str), tier_df["prestamos"])])
    ax.yaxis.set_major_formatter(PercentFormatter(1.0, decimals=0))
    ax.set_xlabel("Nivel de riesgo asignado por el modelo")
    _style(ax, "Incumplimiento real por nivel de riesgo", "Muestra de prueba, usuarios no vistos en el entrenamiento")
    _save(fig, path)


def cutoff(policy: pd.DataFrame, best: pd.Series, path: Path):
    fig, ax = plt.subplots(figsize=(8, 4.5))
    sweep = policy[policy["corte_pd"] < 1.0]
    full = policy[policy["corte_pd"] >= 1.0].iloc[0]
    ax.plot(sweep["corte_pd"], sweep["retorno_sobre_capital"], color=C["blue"], linewidth=2,
            label="Retorno sobre capital con ese corte")
    ax.axhline(full["retorno_sobre_capital"], color=C["ink2"], linewidth=1, linestyle="--",
               label="Sin filtro de riesgo (aprobar todo)")
    ax.plot([best["corte_pd"]], [best["retorno_sobre_capital"]], marker="o", markersize=9, color=C["aqua"],
            markeredgecolor=C["surface"], markeredgewidth=2, linestyle="none",
            label=f"Corte propuesto: PD ≤ {best['corte_pd']:.0%}")
    ax.xaxis.set_major_formatter(PercentFormatter(1.0, decimals=0))
    ax.yaxis.set_major_formatter(PercentFormatter(1.0, decimals=1))
    ax.set_xlabel("Corte: solo aprobar si la PD estimada es menor o igual a…")
    ax.set_ylabel("Resultado neto / capital colocado")
    ax.legend(frameon=False, fontsize=9, loc="upper right", labelcolor=C["ink2"])
    _style(ax, "Retorno sobre capital según el corte de riesgo",
           "Simulación sobre préstamos ya aprobados en la muestra de prueba · el retorno por peso sube, el volumen baja")
    _save(fig, path)
