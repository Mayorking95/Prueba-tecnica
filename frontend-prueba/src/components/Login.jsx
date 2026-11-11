import { useState } from 'react'
import { login } from '../lib/api'

export default function Login({ onLogin }) {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(e) {
    e.preventDefault()
    setSubmitting(true)
    setError('')
    try {
      onLogin(await login(username, password))
    } catch (err) {
      setError(err.message)
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <main className="login-wrap">
      <form className="card login" onSubmit={handleSubmit}>
        <h1>Iniciar sesión</h1>
        <label>
          Usuario
          <input value={username} onChange={(e) => setUsername(e.target.value)} autoFocus required />
        </label>
        <label>
          Contraseña
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
        </label>
        {error && <p className="error">{error}</p>}
        <button className="btn" type="submit" disabled={submitting}>
          {submitting ? 'Entrando…' : 'Entrar'}
        </button>
        <div className="hint">
          <p>Cuentas de prueba:</p>
          <ul>
            <li><code>admin</code> / <code>admin123</code> (administrador)</li>
            <li><code>maria</code> / <code>maria123</code> (usuario)</li>
            <li><code>juan</code> / <code>juan123</code> (usuario)</li>
          </ul>
        </div>
      </form>
    </main>
  )
}
