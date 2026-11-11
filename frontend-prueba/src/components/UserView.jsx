import { useState } from 'react'
import StatusBadge from './StatusBadge'
import { formatDate, formatMoney } from '../lib/format'

export default function UserView({ loans, loading, onRequest }) {
  const [amount, setAmount] = useState('')
  const [term, setTerm] = useState('')
  const [error, setError] = useState('')
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(e) {
    e.preventDefault()
    const value = Number(amount)
    if (!Number.isFinite(value) || value <= 0) {
      setError('Ingresa un monto mayor a 0')
      return
    }
    const months = Number(term)
    if (!Number.isInteger(months) || months <= 0) {
      setError('Ingresa un plazo en meses (número entero mayor a 0)')
      return
    }
    setSubmitting(true)
    try {
      await onRequest(value, months)
      setAmount('')
      setTerm('')
      setError('')
    } catch (err) {
      setError(err.message)
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="stack">
      <section className="card">
        <h2>Solicitar préstamo</h2>
        <form className="row" onSubmit={handleSubmit}>
          <label className="grow">
            Monto (COP)
            <input
              type="number"
              min="1"
              step="1"
              placeholder="Ej: 500000"
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
            />
          </label>
          <label className="grow">
            Plazo (meses)
            <input
              type="number"
              min="1"
              max="60"
              step="1"
              placeholder="Ej: 12"
              value={term}
              onChange={(e) => setTerm(e.target.value)}
            />
          </label>
          <button className="btn" type="submit" disabled={submitting}>
            Solicitar
          </button>
        </form>
        {error && <p className="error">{error}</p>}
      </section>

      <section className="card">
        <h2>Historial de solicitudes</h2>
        {loans.length === 0 ? (
          <p className="muted">{loading ? 'Cargando…' : 'Aún no has realizado solicitudes.'}</p>
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Fecha</th>
                  <th>Monto</th>
                  <th>Plazo</th>
                  <th>Estado</th>
                </tr>
              </thead>
              <tbody>
                {loans.map((l) => (
                  <tr key={l.id}>
                    <td>{formatDate(l.createdAt)}</td>
                    <td>{formatMoney(l.amount)}</td>
                    <td>{l.termMonths} meses</td>
                    <td><StatusBadge status={l.status} /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  )
}
