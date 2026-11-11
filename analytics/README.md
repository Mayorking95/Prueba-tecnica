# Análisis de riesgo y rentabilidad de préstamos (Python)

Analiza lo que la app ya captura (monto, plazo, estado, fechas) y responde tres preguntas:

1. **¿Cómo se comporta la aprobación por tramo de monto?**
2. **¿Cuánto ganamos por intereses y cuánto perdemos por incumplimiento?** (amortización de cuota fija)
3. **¿Qué solicitudes son más riesgosas?** (probabilidad de incumplimiento, niveles Bajo/Medio/Alto y un corte de aprobación)

> **Sobre los datos:** la app no registra pagos, tasas ni mora, y hay muy pocos usuarios. Por eso la rentabilidad y el
> riesgo se calculan sobre un portafolio **sintético** (reproducible, semilla fija). Los resultados demuestran el método;
> **no son hallazgos sobre clientes reales**. Los datos reales de la base se usan para el descriptivo y para puntuar el riesgo
> de las solicitudes de forma ilustrativa.

## Ejecutar

```bash
cd analytics
python -m venv .venv
./.venv/Scripts/python.exe -m pip install -r requirements.txt

./.venv/Scripts/python.exe run_analysis.py --source synthetic     
./.venv/Scripts/python.exe run_analysis.py                         
./.venv/Scripts/python.exe -m pytest                               
```

Para incluir la base real, arranca Postgres (`backend-prueba/scripts/db-start.ps1`) y define `PRESTAMOS_CONN` con la misma cadena que usa la API
(estilo Npgsql `Host=…;Port=5433;Database=prestamos;Username=…;Password=…` o una URL `postgresql://…`). Si la base no responde, el análisis
sigue con los datos sintéticos y lo indica en el informe.

Opciones: `--seed 7`, `--users 3000`, `--out ruta`.

## Qué genera (`output/`, no se versiona)

| Archivo | Contenido |
|---|---|
| `informe.md` | Resumen con hallazgos calculados, supuestos, tablas y 8 gráficos |
| `figuras/*.png` | Aprobación, rentabilidad, incumplimiento por tramo; ROC; calibración; coeficientes; niveles de riesgo; corte |
| `tablas/*.csv` | Cada tabla del informe, más los préstamos puntuados |
| `tablas/solicitudes_reales_con_riesgo.csv` | PD y nivel de riesgo de cada solicitud real (ilustrativo) |

## Estructura

```
prestamos_analytics/
  config.py          Supuestos: tasas por tramo, recuperación, variables, niveles de riesgo
  finance.py         Cuota, interés, saldo y resultado realizado (pagó / incumplió)
  features.py        Historial por usuario calculado 
  data_synthetic.py  Generador reproducible con supuestos documentados
  data_db.py         Lectura de loan_requests desde PostgreSQL
  risk_model.py      Regresión logística y gradient boosting, separando por usuario
  analysis.py        Tablas por tramo, por nivel de riesgo y política de corte
  charts.py          Gráficos (paleta validada, una sola escala por eje)
  report.py          Informe Markdown
  pipeline.py        Orquestación
tests/               Finanzas, ausencia de fuga, reproducibilidad, modelo y conexión
```



