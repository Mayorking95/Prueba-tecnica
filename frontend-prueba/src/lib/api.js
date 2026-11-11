// Cliente HTTP del backend .NET. La URL base se puede cambiar con VITE_API_URL.
const BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5118'
const SESSION_KEY = 'session'

// ---------- Sesión (token JWT + datos del usuario) ----------

export function getSession() {
  try {
    const raw = localStorage.getItem(SESSION_KEY)
    if (!raw) return null
    const session = JSON.parse(raw)
    if (new Date(session.expiresAt) <= new Date()) {
      localStorage.removeItem(SESSION_KEY)
      return null
    }
    return session
  } catch {
    return null
  }
}

function saveSession(session) {
  try {
    localStorage.setItem(SESSION_KEY, JSON.stringify(session))
  } catch {
    /* almacenamiento no disponible */
  }
}

export function clearSession() {
  try {
    localStorage.removeItem(SESSION_KEY)
  } catch {
    /* noop */
  }
}

// ---------- Errores ----------

export class ApiError extends Error {
  constructor(message, status) {
    super(message)
    this.status = status
  }
}

/** Convierte un ProblemDetails / ValidationProblemDetails del backend en un mensaje legible. */
function messageFrom(body, status) {
  if (body?.errors) {
    const first = Object.values(body.errors).flat()[0]
    if (first) return first
  }
  if (body?.detail) return body.detail
  if (body?.title) return body.title
  if (status === 401) return 'Tu sesión expiró. Inicia sesión de nuevo.'
  if (status === 403) return 'No tienes permiso para realizar esta acción.'
  return `Error inesperado (${status})`
}

// El App registra aquí qué hacer cuando el token deja de ser válido.
let onUnauthorized = () => {}
export const setUnauthorizedHandler = (fn) => {
  onUnauthorized = fn
}

async function request(path, { method = 'GET', body } = {}) {
  const session = getSession()
  let response
  try {
    response = await fetch(`${BASE_URL}${path}`, {
      method,
      headers: {
        ...(body ? { 'Content-Type': 'application/json' } : {}),
        ...(session ? { Authorization: `Bearer ${session.token}` } : {}),
      },
      body: body ? JSON.stringify(body) : undefined,
    })
  } catch {
    throw new ApiError('No se pudo conectar con el servidor.', 0)
  }

  if (response.status === 204) return null

  const data = await response.json().catch(() => null)
  if (!response.ok) {
    // 401 con sesión abierta = token vencido; en el login es "credenciales incorrectas".
    if (response.status === 401 && session) onUnauthorized()
    throw new ApiError(messageFrom(data, response.status), response.status)
  }
  return data
}

// ---------- Endpoints ----------

export async function login(username, password) {
  const auth = await request('/api/auth/login', { method: 'POST', body: { username, password } })
  const session = {
    token: auth.token,
    expiresAt: auth.expiresAt,
    id: auth.user.id,
    username: auth.user.username,
    name: auth.user.fullName,
    role: auth.user.role, // 'admin' | 'user'
  }
  saveSession(session)
  return session
}

export const getMyLoans = () => request('/api/loans/mine')
export const getAllLoans = () => request('/api/loans')
export const requestLoan = (amount, termMonths) =>
  request('/api/loans', { method: 'POST', body: { amount, termMonths } })
export const approveLoan = (id) => request(`/api/loans/${id}/approve`, { method: 'POST' })
export const rejectLoan = (id) => request(`/api/loans/${id}/reject`, { method: 'POST' })
