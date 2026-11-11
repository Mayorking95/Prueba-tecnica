import { useCallback, useEffect, useState } from 'react'
import Login from './components/Login'
import Header from './components/Header'
import UserView from './components/UserView'
import AdminView from './components/AdminView'
import {
  approveLoan,
  clearSession,
  getAllLoans,
  getMyLoans,
  getSession,
  rejectLoan,
  requestLoan,
  setUnauthorizedHandler,
} from './lib/api'

export default function App() {
  const [session, setSession] = useState(getSession)
  const [loans, setLoans] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const handleLogout = useCallback(() => {
    clearSession()
    setSession(null)
    setLoans([])
    setError('')
  }, [])

  useEffect(() => {
    setUnauthorizedHandler(handleLogout)
  }, [handleLogout])

  const [reloadKey, setReloadKey] = useState(0)
  const refresh = () => setReloadKey((k) => k + 1)

  useEffect(() => {
    if (!session) return
    let cancelled = false
    ;(async () => {
      try {
        const data = session.role === 'admin' ? await getAllLoans() : await getMyLoans()
        if (!cancelled) {
          setLoans(data)
          setError('')
        }
      } catch (e) {
        if (!cancelled) setError(e.message)
      } finally {
        if (!cancelled) setLoading(false)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [session, reloadKey])

  async function run(action) {
    try {
      await action()
      setError('')
    } catch (e) {
      setError(e.message)
    }
    refresh()
  }

  const handleRequest = async (amount, termMonths) => {
    await requestLoan(amount, termMonths)
    refresh()
  }

  function handleLogin(newSession) {
    setLoading(true)
    setSession(newSession)
  }

  if (!session) return <Login onLogin={handleLogin} />

  return (
    <>
      <Header session={session} onLogout={handleLogout} />
      <main className="container">
        {error && <p className="banner-error">{error}</p>}
        {session.role === 'admin' ? (
          <AdminView
            loans={loans}
            loading={loading}
            onApprove={(id) => run(() => approveLoan(id))}
            onReject={(id) => run(() => rejectLoan(id))}
          />
        ) : (
          <UserView loans={loans} loading={loading} onRequest={handleRequest} />
        )}
      </main>
    </>
  )
}
