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
/** Transition forcée par un Admin (SOC-05) : motif transmis à l'API, tracé au journal. */
const forced = (opts) => (opts?.force ? `?force=true&reason=${encodeURIComponent(opts.reason ?? '')}` : '')

const records = (path) => ({
  list: (params = {}) => request(path + '?' + new URLSearchParams(params)),
  get: (id) => request(`${path}/${id}`),
  create: (dto) => request(path, { method: 'POST', body: JSON.stringify(dto) }),
  update: (id, dto, opts) => request(`${path}/${id}${forced(opts)}`, { method: 'PUT', body: JSON.stringify(dto) }),
  remove: (id) => request(`${path}/${id}`, { method: 'DELETE' }),
  transitions: () => request(`${path}/transitions`)
})

export const api = {
  audit: { list: (type, id) => request(`/audit?type=${type}&id=${id}`) },
  login: (username, password) =>
    request('/auth/login', { method: 'POST', body: JSON.stringify({ username, password }) }),
  problems: {
    list: (params = {}) => request('/problems?' + new URLSearchParams(params)),
    get: (id) => request('/problems/' + id),
    create: (dto) => request('/problems', { method: 'POST', body: JSON.stringify(dto) }),
    update: (id, dto, opts) => request('/problems/' + id + forced(opts), { method: 'PUT', body: JSON.stringify(dto) }),
    transitions: () => request('/problems/transitions')
  },
  analyses: {
    create: (pid, dto) => request(`/problems/${pid}/analyses`, { method: 'POST', body: JSON.stringify(dto) }),
    update: (pid, id, dto) => request(`/problems/${pid}/analyses/${id}`, { method: 'PUT', body: JSON.stringify(dto) })
  },
  actions: {
    all: (params = {}) => request('/actions?' + new URLSearchParams(params)),
    create: (pid, dto) => request(`/problems/${pid}/actions`, { method: 'POST', body: JSON.stringify(dto) }),
    update: (id, dto) => request('/actions/' + id, { method: 'PUT', body: JSON.stringify(dto) }),
    requestApproval: id => request(`/actions/${id}/request-approval`, { method: 'POST' }),
    setChange: (id, reference) => request(`/actions/${id}/change`, { method: 'PUT', body: JSON.stringify({ reference }) })
  },
  incidents: records('/incidents'),
  requests: records('/requests'),
  changes: {
    ...records('/changes'),
    schedule: () => request('/changes/schedule'),
    conflicts: (id) => request(`/changes/${id}/conflicts`),
    // CHG-20 : décisions du CAB.
    approvals: (id) => request(`/changes/${id}/approvals`),
    decide: (id, dto) => request(`/changes/${id}/approvals`, { method: 'POST', body: JSON.stringify(dto) })
  },
  // CHG-25 : périodes de gel.
  changeFreezes: {
    list: () => request('/change-freezes'),
    create: dto => request('/change-freezes', { method: 'POST', body: JSON.stringify(dto) }),
    remove: id => request('/change-freezes/' + id, { method: 'DELETE' })
  },
  // CHG-26 : modèles de changement standard.
  standardChanges: {
    list: (all = false) => request('/standard-changes' + (all ? '?all=true' : '')),
    create: dto => request('/standard-changes', { method: 'POST', body: JSON.stringify(dto) }),
    update: (id, dto) => request('/standard-changes/' + id, { method: 'PUT', body: JSON.stringify(dto) })
  },
  configurationItems: {
    impact: (id) => request(`/configuration-items/${id}/impact`),
    impactOf: (type, id) => request(`/configuration-items/impact-of?type=${type}&id=${id}`),
    verify: (id) => request(`/configuration-items/${id}/verify`, { method: 'POST' }),
    ...records('/configuration-items'),
    addRelation: (id, dto) => request(`/configuration-items/${id}/relations`, { method: 'POST', body: JSON.stringify(dto) }),
    removeRelation: (relId) => request('/configuration-items/relations/' + relId, { method: 'DELETE' })
  },
  services: records('/services'),
  // REQ-20 : catalogue de demandes (modèles).
  requestCatalog: {
    list: (all = false) => request('/request-catalog' + (all ? '?all=true' : '')),
    create: dto => request('/request-catalog', { method: 'POST', body: JSON.stringify(dto) }),
    update: (id, dto) => request('/request-catalog/' + id, { method: 'PUT', body: JSON.stringify(dto) }),
    remove: id => request('/request-catalog/' + id, { method: 'DELETE' })
  },
  agreements: {
    ...records('/agreements'),
    review: (id, dto) => request(`/agreements/${id}/reviews`, { method: 'POST', body: JSON.stringify(dto) }),
    compliance: (id, params = {}) => request(`/agreements/${id}/compliance?` + new URLSearchParams(params))
  },
  knowledge: {
    ...records('/knowledge'),
    submit: (id) => request(`/knowledge/${id}/submit`, { method: 'POST' }),
    review: (id, nextReviewDate) => request(`/knowledge/${id}/review`, { method: 'POST', body: JSON.stringify({ nextReviewDate }) }),
    requestArchive: (id, reason) => request(`/knowledge/${id}/archive-request`, { method: 'POST', body: JSON.stringify({ reason }) }),
    usage: (id) => request(`/knowledge/${id}/usage`)
  },
  improvements: records('/improvements'),
  assessments: {
    ...records('/assessments'),
    questionnaire: (id) => request(`/assessments/${id}/questionnaire`),
    answer: (id, requirementId, dto) =>
      request(`/assessments/${id}/responses/${requirementId}`, { method: 'PUT', body: JSON.stringify(dto) }),
    score: (id) => request(`/assessments/${id}/score`),
    export: (id) => request(`/assessments/${id}/export`),
    addEvidence: (id, requirementId, reference) =>
      request(`/assessments/${id}/responses/${requirementId}/evidence`, { method: 'POST', body: JSON.stringify({ reference }) }),
    removeEvidence: (id, evidenceId) => request(`/assessments/${id}/evidence/${evidenceId}`, { method: 'DELETE' }),
    compare: (id, otherId) => request(`/assessments/${id}/compare/${otherId}`),
    createImprovement: (id, code) =>
      request(`/assessments/${id}/improvements`, { method: 'POST', body: JSON.stringify({ code }) })
  },
  projects: {
    ...records('/projects'),
    summary: (id) => request(`/projects/${id}/summary`),
    /** Éléments d'un projet : phases, milestones, deliverables, sprints, workitems, risks, issues. */
    parts: (id, segment) => ({
      list: () => request(`/projects/${id}/${segment}`),
      create: (dto) => request(`/projects/${id}/${segment}`, { method: 'POST', body: JSON.stringify(dto) }),
      update: (partId, dto) => request(`/projects/${id}/${segment}/${partId}`, { method: 'PUT', body: JSON.stringify(dto) }),
      remove: (partId) => request(`/projects/${id}/${segment}/${partId}`, { method: 'DELETE' })
    }),
    approveStage: (id, phaseId) => request(`/projects/${id}/phases/${phaseId}/approve`, { method: 'POST' }),
    burndown: (id, sprintId) => request(`/projects/${id}/sprints/${sprintId}/burndown`)
  },
  compliance: {
    referential: () => request('/compliance/referential'),
    importText: (csv) => request('/compliance/referential/import', { method: 'POST', body: JSON.stringify({ csv }) })
  },
  perimeters: {
    mine: () => request('/perimeters'),
    // Administration › Périmètres (Admin, PER-01, PER-02, PER-12)
    list: () => request('/admin/perimeters'),
    get: (id) => request('/admin/perimeters/' + id),
    create: (dto) => request('/admin/perimeters', { method: 'POST', body: JSON.stringify(dto) }),
    update: (id, dto) => request('/admin/perimeters/' + id, { method: 'PUT', body: JSON.stringify(dto) }),
    remove: (id) => request('/admin/perimeters/' + id, { method: 'DELETE' }),
    restore: (id) => request(`/admin/perimeters/${id}/restore`, { method: 'POST' }),
    purge: (id) => request(`/admin/perimeters/${id}/purge`, { method: 'DELETE' }),
    assign: (id, dto) => request(`/admin/perimeters/${id}/assignments`, { method: 'POST', body: JSON.stringify(dto) }),
    unassign: (assignmentId) => request('/admin/assignments/' + assignmentId, { method: 'DELETE' })
  },
  notifications: {
    list: () => request('/notifications'),
    read: (id) => request(`/notifications/${id}/read`, { method: 'POST' }),
    readAll: () => request('/notifications/read-all', { method: 'POST' })
  },
  comments: {
    list: (type, id) => request(`/comments?type=${type}&id=${id}`),
    create: (dto) => request('/comments', { method: 'POST', body: JSON.stringify(dto) }),
    remove: (id) => request('/comments/' + id, { method: 'DELETE' })
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
  console: { get: (perimeter) => request('/console' + (perimeter ? '?perimeter=' + perimeter : '')) },
  directory: { search: (q) => request('/directory/search?q=' + encodeURIComponent(q)) },
  reports: {
    summary: () => request('/reports/summary'),
    dashboard: (tab, params = {}) => request(`/reports/dashboard/${tab}?` + new URLSearchParams(params))
  }
}
