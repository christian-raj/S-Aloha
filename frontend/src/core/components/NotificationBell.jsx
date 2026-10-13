import React, { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../../api'
import { dateTimeFr } from '../fields'
import { hrefOf } from '../itemTypes'

const Bell = () => (
  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round"
    strokeLinejoin="round" aria-hidden><path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9" /><path d="M10.3 21a1.94 1.94 0 0 0 3.4 0" /></svg>
)

/**
 * Cloche des notifications (SOC-21) : compteur des non lues, liste des dernières ;
 * ouvrir une notification la marque lue et mène à la fiche. Relue chaque minute.
 * `placement` : « side » (pied de la barre latérale) ou « top » (barre mobile).
 */
export default function NotificationBell({ placement = 'side', onNavigate }) {
  const [data, setData] = useState({ unread: 0, items: [] })
  const [open, setOpen] = useState(false)
  const nav = useNavigate()
  const load = () => api.notifications?.list().then(setData).catch(() => {})
  useEffect(() => {
    load()
    const timer = setInterval(load, 60000)
    return () => clearInterval(timer)
  }, [])

  const go = (n) => {
    const done = () => { setOpen(false); onNavigate?.(); nav(hrefOf(n.targetType, n.targetId)); load() }
    n.read ? done() : api.notifications.read(n.id).then(done).catch(done)
  }
  const readAll = () => api.notifications.readAll().then(load).catch(() => {})
  const label = data.unread ? `Notifications : ${data.unread} non lue(s)` : 'Notifications'

  return (
    <div className={'notif ' + placement}>
      <button className="notif-btn" onClick={() => setOpen(o => !o)} aria-label={label} title={label} aria-expanded={open}>
        <Bell />{data.unread > 0 && <span className="notif-count">{data.unread > 99 ? '99+' : data.unread}</span>}
      </button>
      {open && (
        <div className="notif-panel" role="dialog" aria-label="Notifications">
          <div className="notif-head">
            <b>Notifications</b>
            {data.unread > 0 && <button className="btn ghost small" onClick={readAll}>Tout marquer lu</button>}
          </div>
          {data.items.length === 0 && <p className="muted notif-empty">Rien de nouveau.</p>}
          <ul>
            {data.items.map(n => (
              <li key={n.id} className={n.read ? '' : 'unread'}>
                <button onClick={() => go(n)}>
                  <span className="notif-event">{n.event}</span>
                  <span className="notif-title"><b>{n.reference}</b> {n.title}</span>
                  <span className="notif-meta">{n.actorDisplayName} · {dateTimeFr(n.createdAt)}</span>
                </button>
              </li>))}
          </ul>
        </div>
      )}
    </div>
  )
}
