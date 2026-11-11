import StatusBadge from './StatusBadge'
import { formatDate, formatMoney } from '../lib/format'

export default function AdminView({ loans, loading, onApprove, onReject }) {
  const pending = loans
    .filter((l) => l.status === 'pending')
    .sort((a, b) => a.createdAt.localeCompare(b.createdAt))
  const resolved = loans
    .filter((l) => l.status !== 'pending')
    .sort((a, b) => b.resolvedAt.localeCompare(a.resolvedAt))

  return (
    <div className="stack">
      <section className="card">
        <h2>Gestor de solicitudes</h2>
        {pending.length === 0 ? (
          <p className="muted">{loading ? 'Cargando…' : 'No hay solicitudes pendientes.'}</p>
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Usuario</th>
                  <th>Monto</th>
                  <th>Plazo</th>
                  <th>Estado</th>
                  <th>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {pending.map((l) => (
                  <tr key={l.id}>
                    <td>{l.userName}</td>
                    <td>{formatMoney(l.amount)}</td>
                    <td>{l.termMonths} meses</td>
                    <td><StatusBadge status={l.status} /></td>
                    <td className="actions">
                      <button className="btn btn-ok" onClick={() => onApprove(l.id)}>
                        Aprobar
                      </button>
                      <button className="btn btn-danger" onClick={() => onReject(l.id)}>
                        Rechazar
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section className="card">
        <h2>Historial (aprobados y rechazados)</h2>
        {resolved.length === 0 ? (
          <p className="muted">Todavía no hay solicitudes resueltas.</p>
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Usuario</th>
                  <th>Monto</th>
                  <th>Plazo</th>
                  <th>Estado</th>
                  <th>Resuelto</th>
                </tr>
              </thead>
              <tbody>
                {resolved.map((l) => (
                  <tr key={l.id}>
                    <td>{l.userName}</td>
                    <td>{formatMoney(l.amount)}</td>
                    <td>{l.termMonths} meses</td>
                    <td><StatusBadge status={l.status} /></td>
                    <td>{formatDate(l.resolvedAt)}</td>
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
