export default function Header({ session, onLogout }) {
  return (
    <header className="header">
      <div>
        <strong>Préstamos</strong>
        <span className="muted">
          {' '}
          · {session.name} ({session.role === 'admin' ? 'Administrador' : 'Usuario'})
        </span>
      </div>
      <button className="btn btn-outline" onClick={onLogout}>
        Cerrar sesión
      </button>
    </header>
  )
}
