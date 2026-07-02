const API = '/api'

export function getToken() { return sessionStorage.getItem('token') }
export function getUser() {
  const raw = sessionStorage.getItem('user')
  return raw ? JSON.parse(raw) : null
}

async function request(path, options = {}) {
  const res = await fetch(API + path, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(getToken() ? { Authorization: 'Bearer ' + getToken() } : {}),
      ...options.headers
    }
  })
  if (res.status === 401) {
    sessionStorage.clear()
    window.location.href = '/login'
    throw new Error('Session expirée')
  }
  if (!res.ok) {
    const body = await res.json().catch(() => ({}))
    throw new Error(body.message || 'Une erreur est survenue (' + res.status + ')')
  }
  return res.status === 204 ? null : res.json()
}

export const api = {
  login: (username, password) =>
    request('/auth/login', { method: 'POST', body: JSON.stringify({ username, password }) }),
  problems: {
    list: (params = {}) => request('/problems?' + new URLSearchParams(params)),
    get: (id) => request('/problems/' + id),
    create: (dto) => request('/problems', { method: 'POST', body: JSON.stringify(dto) }),
    update: (id, dto) => request('/problems/' + id, { method: 'PUT', body: JSON.stringify(dto) })
  },
  analyses: {
    create: (pid, dto) => request(`/problems/${pid}/analyses`, { method: 'POST', body: JSON.stringify(dto) }),
    update: (pid, id, dto) => request(`/problems/${pid}/analyses/${id}`, { method: 'PUT', body: JSON.stringify(dto) })
  },
  actions: {
    all: (params = {}) => request('/actions?' + new URLSearchParams(params)),
    create: (pid, dto) => request(`/problems/${pid}/actions`, { method: 'POST', body: JSON.stringify(dto) }),
    update: (id, dto) => request('/actions/' + id, { method: 'PUT', body: JSON.stringify(dto) })
  },
  console: { get: () => request('/console') },
  directory: { search: (q) => request('/directory/search?q=' + encodeURIComponent(q)) },
  reports: { summary: () => request('/reports/summary') }
}
