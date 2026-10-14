import React from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { AgreementReviews } from './AgreementDetail'

const review = vi.fn(() => Promise.resolve({}))
let role = 'Manager'
vi.mock('../../../api', () => ({
  getUser: () => ({ username: 'lova.rabe', role }),
  api: { agreements: { review: (...a) => review(...a) }, perimeters: { mine: () => Promise.resolve([]) } }
}))
vi.mock('../../../core/usePerimeters', () => ({ default: () => [] }))

const agreement = {
  id: 4, perimeterId: 1, status: 'En vigueur', reviewDate: '2027-10-14T00:00:00Z',
  reviews: [{ id: 1, reviewedAt: '2026-10-14T09:00:00Z', reviewedBy: 'Lova Rabe', nextReviewDate: '2027-10-14T00:00:00Z',
    findings: 'Délais P3 tenus à 92 %', decisions: '' }]
}

afterEach(() => { cleanup(); review.mockClear(); role = 'Manager' })

describe('Revues du SLA (SLM-22)', () => {
  it('affiche l\'historique et enregistre une revue', async () => {
    const onSaved = vi.fn()
    render(<AgreementReviews agreement={agreement} onSaved={onSaved} />)
    expect(screen.getByText(/Délais P3 tenus à 92 %/)).toBeTruthy()
    fireEvent.change(screen.getByLabelText('Décisions'), { target: { value: 'Renforcer l\'astreinte' } })
    fireEvent.click(screen.getByText('Enregistrer la revue'))
    await waitFor(() => expect(onSaved).toHaveBeenCalled())
    expect(review).toHaveBeenCalledWith(4, { findings: '', decisions: 'Renforcer l\'astreinte', nextReviewDate: null })
  })

  it('un utilisateur voit les revues sans pouvoir en enregistrer', () => {
    role = 'User'
    render(<AgreementReviews agreement={agreement} onSaved={() => {}} />)
    expect(screen.getByText(/Délais P3 tenus à 92 %/)).toBeTruthy()
    expect(screen.queryByText('Enregistrer la revue')).toBeNull()
  })
})
