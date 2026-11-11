"""Modelo de probabilidad de incumplimiento (PD) con variables disponibles al solicitar."""

from __future__ import annotations

from dataclasses import dataclass

import numpy as np
import pandas as pd
from sklearn.ensemble import HistGradientBoostingClassifier
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import average_precision_score, brier_score_loss, roc_auc_score, roc_curve
from sklearn.model_selection import GroupShuffleSplit
from sklearn.pipeline import make_pipeline
from sklearn.preprocessing import StandardScaler

from . import config


@dataclass
class RiskModel:
    logistic: object
    boosting: object  
    metrics: pd.DataFrame
    coefficients: pd.Series
    roc: dict
    calibration: pd.DataFrame
    test_index: pd.Index

    def predict_pd(self, df: pd.DataFrame) -> np.ndarray:
        return self.logistic.predict_proba(df[config.FEATURES])[:, 1]


def labelled(df: pd.DataFrame) -> pd.DataFrame:
    """Préstamos aprobados con desenlace conocido (los únicos que sirven para entrenar)."""
    return df[(df["status"] == "approved") & df["defaulted"].notna()]


def train(df: pd.DataFrame, seed: int = 7, test_size: float = 0.3) -> RiskModel:
    data = labelled(df)
    X, y = data[config.FEATURES], data["defaulted"].astype(int)

    splitter = GroupShuffleSplit(n_splits=1, test_size=test_size, random_state=seed)
    train_idx, test_idx = next(splitter.split(X, y, groups=data["user_id"]))
    X_tr, X_te, y_tr, y_te = X.iloc[train_idx], X.iloc[test_idx], y.iloc[train_idx], y.iloc[test_idx]

    logistic = make_pipeline(StandardScaler(), LogisticRegression(max_iter=1000))
    boosting = HistGradientBoostingClassifier(max_depth=3, learning_rate=0.05, max_iter=200, random_state=seed)
    logistic.fit(X_tr, y_tr)
    boosting.fit(X_tr, y_tr)

    rows, roc = [], {}
    for name, model in [("Regresión logística", logistic), ("Gradient boosting", boosting)]:
        p = model.predict_proba(X_te)[:, 1]
        fpr, tpr, _ = roc_curve(y_te, p)
        roc[name] = (fpr, tpr)
        rows.append(
            {
                "modelo": name,
                "AUC": roc_auc_score(y_te, p),
                "Precisión media (AP)": average_precision_score(y_te, p),
                "Brier": brier_score_loss(y_te, p),
            }
        )
    metrics = pd.DataFrame(rows)
    metrics.loc[len(metrics)] = {
        "modelo": "Sin modelo (tasa base)",
        "AUC": 0.5,
        "Precisión media (AP)": y_te.mean(),
        "Brier": brier_score_loss(y_te, np.full(len(y_te), y_tr.mean())),
    }

    coefs = pd.Series(logistic[-1].coef_[0], index=config.FEATURES).sort_values()

    p_te = logistic.predict_proba(X_te)[:, 1]
    cal = pd.DataFrame({"pred": p_te, "obs": y_te.to_numpy()})
    cal["decil"] = pd.qcut(cal["pred"], q=8, duplicates="drop")
    calibration = cal.groupby("decil", observed=True).agg(pred=("pred", "mean"), obs=("obs", "mean"), n=("obs", "size"))

    return RiskModel(logistic, boosting, metrics, coefs, roc, calibration.reset_index(drop=True), data.index[test_idx])


def add_tiers(df: pd.DataFrame, pd_col: str = "pd_hat") -> pd.DataFrame:
    out = df.copy()
    out["risk_tier"] = pd.cut(out[pd_col], bins=config.TIER_EDGES, labels=config.TIER_LABELS, include_lowest=True)
    return out
