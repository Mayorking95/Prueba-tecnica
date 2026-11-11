"""Ejecuta el análisis completo.

    python run_analysis.py                      # sintético + lee la BD real si PRESTAMOS_CONN está definida
    python run_analysis.py --source synthetic   # solo sintético
    python run_analysis.py --seed 7 --users 3000
"""

from __future__ import annotations

import argparse
from pathlib import Path

from prestamos_analytics.pipeline import run


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--source", choices=["synthetic", "db", "both"], default="both")
    ap.add_argument("--seed", type=int, default=42)
    ap.add_argument("--users", type=int, default=1500)
    ap.add_argument("--out", type=Path, default=Path(__file__).parent / "output")
    args = ap.parse_args()

    result = run(args.out, source=args.source, seed=args.seed, n_users=args.users)
    best = result["best"]
    print(f"Informe: {result['report_path']}")
    print(f"Solicitudes sintéticas: {len(result['synthetic']):,} | aprobadas con desenlace: {len(result['approved']):,}")
    print(result["model"].metrics.round(3).to_string(index=False))
    print(f"Corte de PD propuesto: {best['corte_pd']:.0%}")
    if result["real_error"]:
        print(f"[BD real no disponible] {result['real_error']}")


if __name__ == "__main__":
    main()
