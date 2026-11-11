"""Orquesta el análisis completo y escribe tablas, gráficos y el informe."""

from __future__ import annotations

from pathlib import Path

import pandas as pd

from . import analysis, charts, config, data_db, data_synthetic, features, finance, risk_model
from .report import build_report
from .report_html import build_html


def prepare(df: pd.DataFrame) -> pd.DataFrame:
    """Historial + tramo + tasa supuesta + cuota/interés programado."""
    return finance.add_economics(features.add_history_features(df))


def run(out_dir: Path, source: str = "both", seed: int = 42, n_users: int = 1500, conn: str | None = None) -> dict:
    out_dir.mkdir(parents=True, exist_ok=True)
    fig_dir = out_dir / "figuras"
    fig_dir.mkdir(exist_ok=True)

    syn = prepare(data_synthetic.generate(n_users=n_users, seed=seed))
    approved = finance.add_realized_outcome(risk_model.labelled(syn))
    syn_desc = analysis.by_band_descriptive(syn)
    eco = analysis.economics_by_band(approved)

    model = risk_model.train(syn, seed=seed)
    approved = approved.assign(pd_hat=model.predict_pd(approved))
    approved = risk_model.add_tiers(approved)

    test = approved.loc[approved.index.intersection(model.test_index)]
    tiers = analysis.economics_by_tier(test)
    policy = analysis.cutoff_policy(test)
    best = policy.loc[(policy["corte_pd"] - config.TIER_EDGES[1]).abs().idxmin()]

    real = real_desc = real_scored = None
    real_error = None
    if source in ("db", "both"):
        try:
            real = prepare(data_db.load_loans(conn))
            real_desc = analysis.by_band_descriptive(real) if len(real) else None
            real["pd_hat"] = model.predict_pd(real)
            real_scored = risk_model.add_tiers(real)
        except Exception as exc: 
            real_error = f"{type(exc).__name__}: {exc}"

    charts.approval_by_band(syn_desc, fig_dir / "01_aprobacion_por_tramo.png")
    charts.economics_by_band(eco, fig_dir / "02_rentabilidad_por_tramo.png")
    charts.default_by_band(eco, fig_dir / "03_incumplimiento_por_tramo.png")
    charts.roc(model.roc, model.metrics, fig_dir / "04_roc.png")
    charts.calibration(model.calibration, fig_dir / "05_calibracion.png")
    charts.coefficients(model.coefficients, fig_dir / "06_coeficientes.png")
    charts.tiers(tiers, fig_dir / "07_niveles_de_riesgo.png")
    charts.cutoff(policy, best, fig_dir / "08_corte_de_riesgo.png")

    tables = out_dir / "tablas"
    tables.mkdir(exist_ok=True)
    syn_desc.to_csv(tables / "descriptivo_por_tramo.csv", index=False)
    eco.to_csv(tables / "rentabilidad_por_tramo.csv", index=False)
    tiers.to_csv(tables / "niveles_de_riesgo.csv", index=False)
    policy.to_csv(tables / "politica_de_corte.csv", index=False)
    model.metrics.to_csv(tables / "metricas_modelo.csv", index=False)
    approved.drop(columns=["user_name"]).to_csv(tables / "prestamos_sinteticos_puntuados.csv", index=False)
    if real_scored is not None and len(real_scored):
        real_scored[
            ["id", "user_name", "amount", "term_months", "status", "annual_rate", "installment", "scheduled_interest", "pd_hat", "risk_tier"]
        ].to_csv(tables / "solicitudes_reales_con_riesgo.csv", index=False)

    report = build_report(
        syn=syn, syn_desc=syn_desc, eco=eco, model=model, tiers=tiers, policy=policy, best=best,
        real=real, real_desc=real_desc, real_scored=real_scored, real_error=real_error,
        seed=seed, n_users=n_users, approved=approved, test=test,
    )
    (out_dir / "informe.md").write_text(report, encoding="utf-8")
    build_html(out_dir / "informe.md", out_dir / "informe.html")

    return {"synthetic": syn, "approved": approved, "economics": eco, "model": model, "tiers": tiers,
            "policy": policy, "best": best, "real": real, "real_error": real_error, "report_path": out_dir / "informe.md"}
