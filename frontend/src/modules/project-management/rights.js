import { getUser } from '../../api'

/** PRJ-04 : chef de projet (responsable, compte utilisateur) ou gestionnaire. */
export const isLead = (project) => {
  const u = getUser()
  if (['Manager', 'Admin'].includes(u?.role)) return true
  return project.ownerType !== 'Group' && !!project.ownerId
    && project.ownerId.toLowerCase() === (u?.username ?? '').toLowerCase()
}
