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

/** Client CRUD d'un processus servi par le RecordController commun de l'API. */
const records = (path) => ({
  list: (params = {}) => request(path + '?' + new URLSearchParams(params)),
  get: (id) => request(`${path}/${id}`),
  create: (dto) => request(path, { method: 'POST', body: JSON.stringify(dto) }),
  update: (id, dto) => request(`${path}/${id}`, { method: 'PUT', body: JSON.stringify(dto) }),
  remove: (id) => request(`${path}/${id}`, { method: 'DELETE' })
})

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
  incidents: records('/incidents'),
  requests: records('/requests'),
  changes: {
    ...records('/changes'),
    schedule: () => request('/changes/schedule'),
    conflicts: (id) => request(`/changes/${id}/conflicts`)
  },
  configurationItems: {
    impact: (id) => request(`/configuration-items/${id}/impact`),
    ...records('/configuration-items'),
    addRelation: (id, dto) => request(`/configuration-items/${id}/relations`, { method: 'POST', body: JSON.stringify(dto) }),
    removeRelation: (relId) => request('/configuration-items/relations/' + relId, { method: 'DELETE' })
  },
  services: records('/services'),
  agreements: records('/agreements'),
  knowledge: records('/knowledge'),
  improvements: records('/improvements'),
  assessments: {
    ...records('/assessments'),
    questionnaire: (id) => request(`/assessments/${id}/questionnaire`),
    answer: (id, requirementId, dto) =>
      request(`/assessments/${id}/responses/${requirementId}`, { method: 'PUT', body: JSON.stringify(dto) }),
    score: (id) => request(`/assessments/${id}/score`),
    createImprovement: (id, code) =>
      request(`/assessments/${id}/improvements`, { method: 'POST', body: JSON.stringify({ code }) })
  },
  compliance: {
    referential: () => request('/compliance/referential'),
    importText: (csv) => request('/compliance/referential/import', { method: 'POST', body: JSON.stringify({ csv }) })
  },
  links: {
    list: (type, id) => request(`/links?type=${type}&id=${id}`),
    create: (dto) => request('/links', { method: 'POST', body: JSON.stringify(dto) }),
    remove: (id) => request('/links/' + id, { method: 'DELETE' })
  },
  search: {
    query: (q, types) => request('/search?' + new URLSearchParams({ q, ...(types ? { types } : {}) })),
    similar: (type, id) => request('/search/similar?' + new URLSearchParams({ type, id })),
    status: () => request('/search/status'),
    reindex: () => request('/search/reindex', { method: 'POST' })
  },
  console: { get: () => request('/console') },
  directory: { search: (q) => request('/directory/search?q=' + encodeURIComponent(q)) },
  reports: { summary: () => request('/reports/summary') }
}
