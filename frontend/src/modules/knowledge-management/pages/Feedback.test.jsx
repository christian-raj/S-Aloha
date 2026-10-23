import React from 'react'
import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { Feedback, Versions } from './ArticleDetail'

const feedback = vi.fn((id, helpful) => Promise.resolve({ myVote: helpful, view: { helpfulCount: helpful ? 3 : 2, notHelpfulCount: helpful ? 1 : 2 } }))
vi.mock('../../../api', () => ({
  getUser: () => ({ role: 'User' }),
  api: { knowledge: {
    myFeedback: () => Promise.resolve({ myVote: null }), feedback: (...a) => feedback(...a),
    versions: () => Promise.resolve([
      { version: 2, title: 'VPN', content: 'Nouvelle procédure', authors: 'fara.rasoa', publishedBy: 'Chef', publishedAt: '2026-10-23T08:00:00Z' },
      { version: 1, title: 'VPN', content: 'Ancienne procédure', authors: 'fara.rasoa', publishedBy: 'Chef', publishedAt: '2026-10-01T08:00:00Z' }]) } }
}))
afterEach(() => { cleanup(); feedback.mockClear() })

it('KB-13 : le lecteur vote sur un article publié ; rien sur un brouillon', async () => {
  render(<Feedback article={{ id: 4, status: 'Publié', version: 1, helpfulCount: 2, notHelpfulCount: 1 }} />)
  fireEvent.click(screen.getByText('Utile (2)'))
  await waitFor(() => expect(feedback).toHaveBeenCalledWith(4, true))
  expect((await screen.findByText('Utile (3)')).getAttribute('aria-pressed')).toBe('true')
  cleanup()
  const { container } = render(<Feedback article={{ id: 5, status: 'Brouillon', version: 0, helpfulCount: 0, notHelpfulCount: 0 }} />)
  expect(container.textContent).toBe('')
})

it('KB-15 : les versions publiées se relisent', async () => {
  render(<Versions article={{ id: 4, version: 2 }} />)
  fireEvent.click(await screen.findByText('Version 1'))
  expect(await screen.findByText('Ancienne procédure')).toBeTruthy()
})
