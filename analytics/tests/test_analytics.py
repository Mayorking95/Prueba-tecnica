import numpy as np
import pandas as pd
import pytest

from prestamos_analytics import config, data_db, data_synthetic, features, finance, risk_model


# ---------- Matemática financiera ----------

def test_installment_known_value():
    # 10M a 12 meses, 24% efectivo anual: cuota calculada a mano con la tasa mensual (1.24^(1/12) - 1).
    r = 1.24 ** (1 / 12) - 1
    expected = 10_000_000 * r / (1 - (1 + r) ** -12)
    assert finance.installment(10_000_000, 0.24, 12) == pytest.approx(expected)


def test_total_interest_equals_installments_minus_principal():
    pmt = finance.installment(5_000_000, 0.26, 24)
    assert finance.total_interest(5_000_000, 0.26, 24) == pytest.approx(pmt * 24 - 5_000_000)
    assert finance.total_interest(5_000_000, 0.26, 24) > 0


def test_balance_is_principal_at_start_and_zero_at_end():
    assert finance.balance_after(8_000_000, 0.22, 36, 0) == pytest.approx(8_000_000)
    assert finance.balance_after(8_000_000, 0.22, 36, 36) == pytest.approx(0, abs=1e-4)


def test_interest_collected_plus_capital_equals_payments():
    k = 10
    pmt = finance.installment(3_000_000, 0.26, 18)
    capital = 3_000_000 - finance.balance_after(3_000_000, 0.26, 18, k)
    assert finance.interest_collected(3_000_000, 0.26, 18, k) + capital == pytest.approx(pmt * k)


def test_interest_collected_over_full_term_equals_total_interest():
    assert finance.interest_collected(2_000_000, 0.3, 12, 12) == pytest.approx(finance.total_interest(2_000_000, 0.3, 12))


@pytest.mark.parametrize(
    "amount,band",
    [(100_000, "<1M"), (999_999, "<1M"), (1_000_000, "1M-5M"), (5_000_000, "5M-20M"), (20_000_000, ">20M"), (50_000_000, ">20M")],
)
def test_band_edges(amount, band):
    assert str(finance.band_for(pd.Series([amount])).iloc[0]) == band


def test_realized_outcome_paid_vs_default():
    df = finance.add_economics(
        pd.DataFrame(
            {
                "amount": [2_000_000.0, 2_000_000.0],
                "term_months": [12, 12],
                "defaulted": [0.0, 1.0],
                "default_month": [np.nan, 4.0],
            }
        )
    )
    out = finance.add_realized_outcome(df)
    paid, defaulted = out.iloc[0], out.iloc[1]

    assert paid["loss"] == 0
    assert paid["net_result"] == pytest.approx(paid["scheduled_interest"])
    # El incumplimiento pierde saldo no recuperado y cobra menos interés que el programado.
    assert defaulted["loss"] > 0
    assert defaulted["interest_collected"] < defaulted["scheduled_interest"]
    assert defaulted["net_result"] < paid["net_result"]
    expected_loss = finance.balance_after(2_000_000, 0.26, 12, 4) * (1 - config.RECOVERY_RATE)
    assert defaulted["loss"] == pytest.approx(expected_loss)


# ---------- Variables sin fuga de información ----------

def test_history_features_only_use_the_past():
    t = pd.date_range("2025-01-01", periods=4, freq="30D", tz="UTC")
    df = pd.DataFrame(
        {
            "id": [1, 2, 3, 4],
            "user_id": [1, 1, 1, 1],
            "amount": [1_000_000.0, 2_000_000.0, 3_000_000.0, 4_000_000.0],
            "status": ["approved", "rejected", "approved", "approved"],
            "created_at": t,
            "defaulted": [1.0, np.nan, 0.0, 0.0],
        }
    )
    out = features.add_history_features(df).set_index("id")

    assert out["prior_loans"].tolist() == [0, 1, 2, 3]
    # El primer préstamo no ve su propio incumplimiento; los siguientes sí ven el pasado.
    assert out["prior_defaults"].tolist() == [0, 1, 1, 1]
    assert out["prior_rejections"].tolist() == [0, 0, 1, 1]
    # Sin historial la razón es 1; luego compara contra el promedio PREVIO.
    assert out.loc[1, "amount_ratio"] == 1.0
    assert out.loc[2, "amount_ratio"] == pytest.approx(2.0)
    assert out.loc[3, "amount_ratio"] == pytest.approx(3_000_000 / 1_500_000)


def test_history_features_work_without_default_column():
    df = pd.DataFrame(
        {
            "id": [1, 2],
            "user_id": [1, 1],
            "amount": [1_000_000.0, 1_000_000.0],
            "status": ["approved", "pending"],
            "created_at": pd.date_range("2025-01-01", periods=2, tz="UTC"),
        }
    )
    assert features.add_history_features(df)["prior_defaults"].tolist() == [0, 0]


# ---------- Datos sintéticos y modelo ----------

def test_synthetic_data_is_reproducible_and_respects_business_rules():
    a = data_synthetic.generate(n_users=200, seed=1)
    b = data_synthetic.generate(n_users=200, seed=1)
    pd.testing.assert_frame_equal(a, b)

    assert a["amount"].between(100_000, 50_000_000).all()
    assert a["term_months"].between(1, 60).all()
    assert set(a["status"]) <= {"approved", "rejected"}
    # Solo los aprobados tienen desenlace.
    assert a.loc[a["status"] == "rejected", "defaulted"].isna().all()
    assert a.loc[a["status"] == "approved", "defaulted"].notna().all()
    assert (a["resolved_at"] > a["created_at"]).all()


@pytest.fixture(scope="module")
def prepared():
    df = data_synthetic.generate(n_users=700, seed=3)
    return finance.add_economics(features.add_history_features(df))


def test_model_beats_chance_on_unseen_users(prepared):
    model = risk_model.train(prepared, seed=3)
    auc = model.metrics.set_index("modelo").loc["Regresión logística", "AUC"]
    assert auc > 0.6

    pd_hat = model.predict_pd(prepared)
    assert ((pd_hat > 0) & (pd_hat < 1)).all()


def test_train_and_test_users_do_not_overlap(prepared):
    model = risk_model.train(prepared, seed=3)
    data = risk_model.labelled(prepared)
    test_users = set(data.loc[model.test_index, "user_id"])
    train_users = set(data.drop(model.test_index)["user_id"])
    assert not test_users & train_users


# ---------- Conexión a la BD ----------

def test_npgsql_connection_string_is_translated():
    raw = "Host=localhost;Port=5433;Database=prestamos;Username=postgres;Password=s3cr3t"
    info = data_db.to_conninfo(raw)
    assert "host=localhost" in info and "port=5433" in info and "dbname=prestamos" in info
    assert "user=postgres" in info and "password=s3cr3t" in info


def test_url_connection_string_is_kept():
    url = "postgresql://u:p@localhost:5433/prestamos"
    assert data_db.to_conninfo(url) == url


def test_load_loans_requires_connection(monkeypatch):
    monkeypatch.delenv(data_db.ENV_VAR, raising=False)
    with pytest.raises(RuntimeError):
        data_db.load_loans()
