"""Informe en Markdown con hallazgos calculados (no escritos a mano) y supuestos a la vista."""

from __future__ import annotations

import pandas as pd

from . import config
from .charts import LABELS


def _money(v: float) -> str:
    return f"${v / 1e6:,.1f} M"


def _table(df: pd.DataFrame, formats: dict | None = None) -> str:
    formats = formats or {}
    cols = list(df.columns)
    lines = ["| " + " | ".join(cols) + " |", "|" + "|".join(["---"] * len(cols)) + "|"]
    for _, row in df.iterrows():
        cells = []
        for c in cols:
            v = row[c]
            f = formats.get(c)
            cells.append(f(v) if f and pd.notna(v) else ("" if pd.isna(v) else str(v)))
        lines.append("| " + " | ".join(cells) + " |")
    return "\n".join(lines)


pct = lambda v: f"{v:.1%}"  # noqa: E731
num = lambda v: f"{v:,.0f}"  # noqa: E731


def build_report(*, syn, syn_desc, eco, model, tiers, policy, best, real, real_desc, real_scored, real_error,
                 seed, n_users, approved, test) -> str:
    full = policy[policy["corte_pd"] >= 1.0].iloc[0]
    MIN_N = 100
    reliable = eco[eco["prestamos"] >= MIN_N]
    small = eco[eco["prestamos"] < MIN_N]
    best_band = reliable.loc[reliable["retorno_sobre_capital"].idxmax()]
    worst_band = reliable.loc[reliable["retorno_sobre_capital"].idxmin()]
    auc = model.metrics.loc[model.metrics["modelo"] == "Regresión logística", "AUC"].iloc[0]
    base_rate = approved["defaulted"].mean()
    top_risk = model.coefficients.abs().idxmax()
    low, high = tiers.iloc[0], tiers.iloc[-1]

    lost_net = full["resultado_neto"] - best["resultado_neto"]
    out = []
    out.append("# Análisis de riesgo y rentabilidad de préstamos\n")
    out.append(
        "> **Importante:** la app registra monto, plazo, estado y fechas, pero **no** pagos, tasas ni mora. "
        "Por eso la rentabilidad y el riesgo se calculan sobre un portafolio **sintético** (semilla "
        f"{seed}, {n_users:,} usuarios, {len(syn):,} solicitudes) con supuestos declarados abajo. "
        "Las cifras demuestran el método; no describen clientes reales.\n"
    )

    out.append("## 1. Resumen\n")
    out.append(f"- **Mejor tramo por retorno:** {best_band['band']} con {pct(best_band['retorno_sobre_capital'])} del capital colocado "
               f"(resultado neto {_money(best_band['resultado_neto'])}).")
    out.append(f"- **Peor tramo por retorno:** {worst_band['band']} con {pct(worst_band['retorno_sobre_capital'])} "
               f"(incumplimiento {pct(worst_band['tasa_incumplimiento'])}).")
    out.append(f"- **Incumplimiento global** en préstamos aprobados: {pct(base_rate)}.")
    out.append(f"- **Modelo de riesgo:** AUC {auc:.3f} en usuarios no vistos (0.5 = azar). "
               f"La variable de mayor peso es *{LABELS.get(top_risk, top_risk)}*.")
    out.append(f"- **Los niveles de riesgo ordenan bien:** incumplimiento {pct(low['tasa_incumplimiento'])} en nivel {low['risk_tier']} "
               f"frente a {pct(high['tasa_incumplimiento'])} en nivel {high['risk_tier']}.")
    out.append(f"- **Política de corte:** aprobar solo con PD ≤ {best['corte_pd']:.0%} sube el retorno sobre capital de "
               f"{pct(full['retorno_sobre_capital'])} a {pct(best['retorno_sobre_capital'])}, pero coloca solo el "
               f"{pct(best['capital_colocado_pct'])} del capital y deja de ganar {_money(lost_net)} en términos absolutos. "
               "Conviene cuando el capital es el recurso escaso; si no, aprobar más sigue sumando.")
    if len(small):
        names = ", ".join(f"{r['band']} (n={int(r['prestamos'])})" for _, r in small.iterrows())
        out.append(f"- **Tramos sin ranquear por muestra pequeña (< {MIN_N} préstamos):** {names}. Su retorno es ruido, no señal.")
    out.append("")

    out.append("## 2. Supuestos (todo lo que NO sale de la app)\n")
    out.append("Tasas efectivas anuales por tramo de monto (supuesto):\n")
    rates = pd.DataFrame({"Tramo": config.BAND_LABELS,
                          "Tasa efectiva anual": [config.ANNUAL_RATE_BY_BAND[b] for b in config.BAND_LABELS]})
    out.append(_table(rates, {"Tasa efectiva anual": lambda v: f"{v:.0%}"}))
    out.append(f"\n- Amortización de cuota fija (sistema francés).\n- Recuperación tras incumplimiento: {config.RECOVERY_RATE:.0%} del saldo pendiente.")
    out.append("- Incumplimiento: ocurre en un mes aleatorio entre la cuota 1 y la penúltima; se pierde el saldo no recuperado.")
    out.append("- Solo los préstamos **aprobados** tienen desenlace; no se sabe qué habría pasado con los rechazados (sesgo de selección).")
    out.append("- Variables del modelo (solo información disponible al solicitar): "
               + ", ".join(f"`{f}`" for f in config.FEATURES) + ".\n")

    out.append("## 3. Aprobación por tramo\n")
    out.append("![Aprobación](figuras/01_aprobacion_por_tramo.png)\n")
    out.append(_table(syn_desc.rename(columns={
        "band": "Tramo", "solicitudes": "Solicitudes", "monto_promedio": "Monto prom.", "plazo_promedio": "Plazo prom. (m)",
        "tasa_aprobacion": "Aprobación", "horas_resolucion_mediana": "Horas resolución (mediana)"}),
        {"Solicitudes": num, "Monto prom.": lambda v: f"${v:,.0f}", "Plazo prom. (m)": lambda v: f"{v:.1f}",
         "Aprobación": pct, "Horas resolución (mediana)": lambda v: f"{v:.1f}"}))

    out.append("\n## 4. Rentabilidad por tramo\n")
    out.append("![Rentabilidad](figuras/02_rentabilidad_por_tramo.png)\n")
    out.append("![Incumplimiento](figuras/03_incumplimiento_por_tramo.png)\n")
    out.append(_table(eco.rename(columns={
        "band": "Tramo", "prestamos": "Préstamos", "capital": "Capital", "interes_programado": "Interés programado",
        "interes_cobrado": "Interés cobrado", "perdidas": "Pérdidas", "resultado_neto": "Resultado neto",
        "tasa_incumplimiento": "Incumplimiento", "retorno_sobre_capital": "Retorno / capital"}),
        {"Préstamos": num, "Capital": _money, "Interés programado": _money, "Interés cobrado": _money,
         "Pérdidas": _money, "Resultado neto": _money, "Incumplimiento": pct, "Retorno / capital": pct}))

    out.append("\n## 5. Modelo de riesgo\n")
    out.append(_table(model.metrics, {"AUC": lambda v: f"{v:.3f}", "Precisión media (AP)": lambda v: f"{v:.3f}", "Brier": lambda v: f"{v:.4f}"}))
    out.append("\nSe elige la regresión logística: rinde parecido al boosting y se puede explicar a un administrador.\n")
    out.append("![ROC](figuras/04_roc.png)\n")
    out.append("![Calibración](figuras/05_calibracion.png)\n")
    out.append("![Coeficientes](figuras/06_coeficientes.png)\n")

    out.append("## 6. Niveles de riesgo y política de corte\n")
    out.append(f"Niveles por PD estimada: Bajo < {config.TIER_EDGES[1]:.0%}, Medio < {config.TIER_EDGES[2]:.0%}, Alto en adelante.\n")
    out.append("![Niveles](figuras/07_niveles_de_riesgo.png)\n")
    out.append(_table(tiers.rename(columns={
        "risk_tier": "Nivel", "prestamos": "Préstamos", "pd_promedio": "PD prom.", "tasa_incumplimiento": "Incumplimiento real",
        "capital": "Capital", "interes_cobrado": "Interés cobrado", "perdidas": "Pérdidas", "resultado_neto": "Resultado neto",
        "retorno_sobre_capital": "Retorno / capital"}),
        {"Préstamos": num, "PD prom.": pct, "Incumplimiento real": pct, "Capital": _money, "Interés cobrado": _money,
         "Pérdidas": _money, "Resultado neto": _money, "Retorno / capital": pct}))
    out.append("\n![Corte](figuras/08_corte_de_riesgo.png)\n")
    out.append(f"Con corte en PD ≤ {best['corte_pd']:.0%}: {int(best['prestamos'])} préstamos, retorno {pct(best['retorno_sobre_capital'])}, "
               f"resultado neto {_money(best['resultado_neto'])}. Sin filtro: {int(full['prestamos'])} préstamos, retorno "
               f"{pct(full['retorno_sobre_capital'])}, resultado neto {_money(full['resultado_neto'])}.\n")
    out.append("Atención: el corte se evalúa solo sobre préstamos ya aprobados; no mide el efecto de aprobar solicitudes que se rechazaron. "
               "Además, con las tasas supuestas todos los niveles de riesgo son rentables en conjunto: el modelo sirve más para "
               "**asignar capital y fijar precio** que para rechazar.\n")

    out.append("## 7. Datos reales de la app\n")
    if real_error:
        out.append(f"No se pudo leer la base de datos: `{real_error}`. Comprueba que la base esté encendida (`backend-prueba/scripts/db-start.ps1`) y que `PRESTAMOS_CONN` tenga la cadena de conexión; luego vuelve a ejecutar.\n")
    elif real is None:
        out.append("No se usó la base de datos en esta ejecución (`--source synthetic`).\n")
    elif len(real) == 0:
        out.append("La base está vacía.\n")
    else:
        resolved = real[real["status"].isin(["approved", "rejected"])]
        approved_real = real[real["status"] == "approved"]
        out.append(f"{len(real)} solicitudes · {real['user_id'].nunique()} usuarios · {len(resolved)} resueltas · "
                   f"{(real['status'] == 'pending').sum()} pendientes.\n")
        if real_desc is not None:
            out.append(_table(real_desc.rename(columns={
                "band": "Tramo", "solicitudes": "Solicitudes", "monto_promedio": "Monto prom.", "plazo_promedio": "Plazo prom. (m)",
                "tasa_aprobacion": "Aprobación", "horas_resolucion_mediana": "Horas resolución (mediana)"}).dropna(subset=["Monto prom."]),
                {"Solicitudes": num, "Monto prom.": lambda v: f"${v:,.0f}", "Plazo prom. (m)": lambda v: f"{v:.1f}",
                 "Aprobación": pct, "Horas resolución (mediana)": lambda v: f"{v:.2f}"}))
        out.append(f"\nInterés programado de las {len(approved_real)} solicitudes aprobadas (con las tasas supuestas): "
                   f"**${approved_real['scheduled_interest'].sum():,.0f}** sobre un capital de **${approved_real['amount'].sum():,.0f}**.\n")
        out.append("El puntaje de riesgo por solicitud real está en `tablas/solicitudes_reales_con_riesgo.csv`. "
                   "Es **ilustrativo**: el modelo se entrenó con datos sintéticos y la app aún no registra incumplimientos reales.\n")

    out.append("## 8. Limitaciones\n")
    out.append("- Sin pagos reales no hay validación externa: el modelo recupera la lógica con la que se generaron los datos.")
    out.append("- Las tasas y la recuperación son supuestos; cambiarlos mueve la rentabilidad (ver `config.py`).")
    out.append("- El sesgo de selección impide saber el riesgo de los rechazados.")
    out.append("- Para pasar a datos reales hay que registrar pagos/mora en la app y reentrenar con `--source db`.\n")
    return "\n".join(out)
